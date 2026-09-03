using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RegionToShare.Properties;
using Throttle;
using TomsToolbox.Essentials;
using TomsToolbox.Wpf;
using TomsToolbox.Wpf.Styles;
using static RegionToShare.NativeMethods;
using static RegionToShare.ExtensionMethods;

namespace RegionToShare;

public partial class MainWindow
{
    private IntPtr _separationLayerHandle;

    private IntPtr _windowHandle;
    private RecordingWindow? _recordingWindow;

    private POINT _debugOffset;

    private bool _isMoving;
    private bool _isSizing;
    private bool _isRefreshingAnchorButtons;
    private WallpaperCache? _wallpaper;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        Resolutions = LoadResolutions();
        Resources.RegisterDefaultStyles();
        SetThemeColor();
        ApplyBackgroundColor();
        Settings.PropertyChanged += Settings_PropertyChanged;
        RefreshAnchorButtons();
    }

    public string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public ICollection<string> Resolutions { get; }

    public static ICollection<int> SupportedFramesPerSecond { get; } = new[] { 5, 10, 15, 20, 30, 60 };

    public static ICollection<string> AspectRatios => AspectRatio.Supported;

    internal Settings Settings => Settings.Default;

    public string? Extend
    {
        get => (string?)GetValue(ExtendProperty);
        set => SetValue(ExtendProperty, value);
    }
    public static readonly DependencyProperty ExtendProperty = DependencyProperty.Register(nameof(Extend), typeof(string), typeof(MainWindow),
        new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, args) => ((MainWindow)d).OnExtendChanged(args.NewValue as string)));

    /// <summary>
    /// Solid background color from the settings; used when the wallpaper is off or unavailable.
    /// </summary>
    public Brush BackgroundBrush
    {
        get => (Brush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }
    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register(
        nameof(BackgroundBrush), typeof(Brush), typeof(MainWindow), new PropertyMetadata(Brushes.Black));

    public static IReadOnlyList<NamedColor> BackgroundColorItems => BackgroundColors.Items;

    public static IReadOnlyList<NamedColor> ThemeColorItems => ThemeColors.Items;

    /// <summary>
    /// The solid background color only matters while the wallpaper is off.
    /// </summary>
    public bool IsBackgroundColorEnabled
    {
        get => (bool)GetValue(IsBackgroundColorEnabledProperty);
        set => SetValue(IsBackgroundColorEnabledProperty, value);
    }
    public static readonly DependencyProperty IsBackgroundColorEnabledProperty = DependencyProperty.Register(
        nameof(IsBackgroundColorEnabled), typeof(bool), typeof(MainWindow), new PropertyMetadata(true, (d, _) => ((MainWindow)d).RefreshBackgroundColorToolTip()));

    private void RefreshBackgroundColorToolTip()
    {
        SetValue(BackgroundColorToolTipProperty, BackgroundColorToolTipText);
    }

    public string BackgroundColorToolTip
    {
        get => (string)GetValue(BackgroundColorToolTipProperty);
        set => SetValue(BackgroundColorToolTipProperty, value);
    }
    public static readonly DependencyProperty BackgroundColorToolTipProperty = DependencyProperty.Register(
        nameof(BackgroundColorToolTip), typeof(string), typeof(MainWindow), new PropertyMetadata("Color shown where nothing covers the shared region"));

    private string BackgroundColorToolTipText => IsBackgroundColorEnabled
        ? "Color shown where nothing covers the shared region"
        : "Not available while \"Show desktop wallpaper\" is on";

    /// <summary>
    /// Clicks that land on the options panel (labels, gaps, disabled controls) must not start sharing.
    /// </summary>
    private void OptionsPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void ApplyBackgroundColor()
    {
        var color = BackgroundColors.Get(Settings.BackgroundColor);

        BackgroundBrush = color.Brush;
        Background = color.Brush;
        // On the wallpaper the text stays white; the wallpaper is unknown, the solid color is not.
        Foreground = Settings.ShowDesktopWallpaper ? Brushes.White : color.Foreground;
        IsBackgroundColorEnabled = !Settings.ShowDesktopWallpaper;
        InfoAreaBackground = color.Brush;
        SeparationLayerBackground = color.Brush;

        UpdateInfoAreaBackground();
        SetSeparationLayerPos(SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE);
    }

    /// <summary>
    /// Background of the info area: the wallpaper slice under the window when enabled, else the dot pattern.
    /// </summary>
    public Brush? InfoAreaBackground
    {
        get => (Brush?)GetValue(InfoAreaBackgroundProperty);
        set => SetValue(InfoAreaBackgroundProperty, value);
    }
    public static readonly DependencyProperty InfoAreaBackgroundProperty = DependencyProperty.Register(
        nameof(InfoAreaBackground), typeof(Brush), typeof(MainWindow), new PropertyMetadata(default(Brush)));

    /// <summary>
    /// Background of the separation layer, the window the user actually sees on the desktop while sharing.
    /// </summary>
    public Brush? SeparationLayerBackground
    {
        get => (Brush?)GetValue(SeparationLayerBackgroundProperty);
        set => SetValue(SeparationLayerBackgroundProperty, value);
    }
    public static readonly DependencyProperty SeparationLayerBackgroundProperty = DependencyProperty.Register(
        nameof(SeparationLayerBackground), typeof(Brush), typeof(MainWindow), new PropertyMetadata(default(Brush)));

    internal IntPtr WindowHandle => _windowHandle;

    /// <summary>
    /// Wallpaper slice for a screen rectangle as a brush, or the dot pattern when the wallpaper is off or unavailable.
    /// </summary>
    private Brush? CreateBackgroundBrush(RECT rect)
    {
        if (!Settings.ShowDesktopWallpaper)
            return BackgroundBrush;

        try
        {
            var slice = Wallpaper.GetSlice(rect);

            if (slice == null)
                return BackgroundBrush;

            var bitmapHandle = slice.GetHbitmap();

            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmapHandle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return new ImageBrush(source) { Stretch = Stretch.Fill };
            }
            finally
            {
                DeleteObject(bitmapHandle);
            }
        }
        catch
        {
            return BackgroundBrush;
        }
    }

    private WallpaperCache Wallpaper
    {
        get
        {
            if (_wallpaper == null)
            {
                _wallpaper = new WallpaperCache();
                _wallpaper.Changed += (_, _) =>
                {
                    UpdateInfoAreaBackground();
                    SetSeparationLayerPos(SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE);
                };
            }

            return _wallpaper;
        }
    }

    private void UpdateInfoAreaBackground()
    {
        if (_windowHandle == IntPtr.Zero || _recordingWindow != null)
            return;

        GetClientRect(_windowHandle, out var client);
        var origin = new POINT();
        ClientToScreen(_windowHandle, ref origin);

        InfoAreaBackground = CreateBackgroundBrush(client.Offset(origin.X, origin.Y));
    }

    private void OnExtendChanged(string? newValue)
    {
        if (newValue is null || !TryParseSize(newValue, out var size))
            return;

        if (_recordingWindow == null && WindowState == WindowState.Normal)
        {
            if (AspectRatio.TryParse(Settings.AspectRatio, out var ratio))
            {
                size = AspectRatio.Adjust(size, ratio, false, MinRegionSize);
            }

            ApplyRegionSize(size);
            return;
        }

        size += GlassFrameThickness;
        SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, size.Width, size.Height, SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOMOVE);
    }

    private Transformations DeviceTransformations => (HwndSource.FromHwnd(_windowHandle)?.CompositionTarget).GetDeviceTransformations();

    private SIZE MinRegionSize
    {
        get
        {
            var min = DeviceTransformations.ToDevice.Transform(new Vector(MinWidth, MinHeight));
            return new SIZE((int)Math.Ceiling(min.X), (int)Math.Ceiling(min.Y));
        }
    }

    private int SnapThresholdPx => (int)Math.Round(DeviceTransformations.ToDevice.Transform(new Vector(WindowGeometry.SnapThreshold, 0)).X);

    /// <summary>
    /// Resizes the region (window without the glass frame) and re-applies the anchor; no-op when nothing changes.
    /// While recording the recording window owns the geometry.
    /// </summary>
    private void ApplyRegionSize(SIZE regionSize)
    {
        if (_windowHandle == IntPtr.Zero || _recordingWindow != null || WindowState != WindowState.Normal)
            return;

        var glass = GlassFrameThickness;
        var current = NativeWindowRect;
        var size = regionSize + glass;
        var rect = new RECT { Left = current.Left, Top = current.Top, Right = current.Left + size.Width, Bottom = current.Top + size.Height };

        if (TryGetMonitorInfo(_windowHandle, out var monitor))
        {
            rect = WindowGeometry.ApplyAnchor(rect, glass, monitor.rcWork, WindowGeometry.CurrentAnchor);
        }

        if (rect == current)
            return;

        NativeWindowRect = rect;
    }

    private void ApplyAspectRatioNow()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        var region = NativeWindowRect - GlassFrameThickness;
        var size = new SIZE(region.Width, region.Height);

        if (AspectRatio.TryParse(Settings.AspectRatio, out var ratio))
        {
            size = AspectRatio.Adjust(size, ratio, false, MinRegionSize);
        }

        ApplyRegionSize(size);
    }

    private void ApplyAnchorNow()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        var region = NativeWindowRect - GlassFrameThickness;
        ApplyRegionSize(new SIZE(region.Width, region.Height));
    }

    private void ReleaseAnchor()
    {
        if (Settings.WindowAnchor != (int)WindowAnchor.None)
        {
            Settings.WindowAnchor = (int)WindowAnchor.None;
        }
    }

    // Checked/Unchecked instead of Click, so the buttons also work when toggled via UI automation or keyboard.
    private void AnchorButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isRefreshingAnchorButtons || !TryGetAnchor(sender, out var anchor))
            return;

        Settings.WindowAnchor = anchor;
    }

    private void AnchorButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isRefreshingAnchorButtons || !TryGetAnchor(sender, out var anchor))
            return;

        if (Settings.WindowAnchor == anchor)
        {
            Settings.WindowAnchor = (int)WindowAnchor.None;
        }
    }

    private static bool TryGetAnchor(object sender, out int anchor)
    {
        anchor = (int)WindowAnchor.None;

        return sender is ToggleButton { Tag: string tag } && int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out anchor);
    }

    private void RefreshAnchorButtons()
    {
        var anchor = Settings.WindowAnchor;

        _isRefreshingAnchorButtons = true;

        try
        {
            foreach (var button in AnchorGrid.Children.OfType<ToggleButton>())
            {
                button.IsChecked = TryGetAnchor(button, out var value) && value == anchor;
            }
        }
        finally
        {
            _isRefreshingAnchorButtons = false;
        }
    }

    private IntPtr WindowProc(IntPtr windowHandle, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_SIZING:
                _isSizing = true;
                handled = true;
                return WindowGeometry.HandleSizing(windowHandle, wParam, lParam, GlassFrameThickness, MinRegionSize, SnapThresholdPx);

            case WM_ENTERSIZEMOVE:
                _isMoving = false;
                _isSizing = false;
                break;

            case WM_MOVING:
                _isMoving = true;
                break;

            case WM_EXITSIZEMOVE:
                // Dragging the window by hand releases the anchor, like undocking.
                if (_isMoving && !_isSizing)
                {
                    ReleaseAnchor();
                }
                break;
        }

        return IntPtr.Zero;
    }

    internal Thickness GlassFrameThickness => DwmGetExtendedFrameBounds(_windowHandle);

    internal RECT NativeWindowRect
    {
        get
        {
            GetWindowRect(_windowHandle, out var rect);
            return rect;
        }
        set
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            SetWindowPos(_windowHandle, IntPtr.Zero, value.Left, value.Top, value.Width, value.Height, SWP_NOACTIVATE | SWP_NOZORDER);
        }
    }

    private ICollection<string> LoadResolutions()
    {
        var defaultResolutions = new[] { @"1024x782", @"1280x720", @"1280x1024", @"1600x900", @"1920x1080", @"2560x1440", @"3840x2160" };

        try
        {
            var userDataDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"RegionToShare");
            var resolutionsFilePath = Path.Combine(userDataDirPath, @"resolutions.txt");

            Directory.CreateDirectory(userDataDirPath);

            if (!File.Exists(resolutionsFilePath))
            {
                File.WriteAllLines(resolutionsFilePath, defaultResolutions);
                return defaultResolutions;
            }

            var resolutions = File.ReadAllLines(resolutionsFilePath)
                .Where(item => TryParseSize(item, out _))
                .ToArray();

            // Defaults added in later versions are merged into an existing file, sorted by width then height.
            var merged = resolutions.Union(defaultResolutions)
                .OrderBy(item => TryParseSize(item, out var size) ? size.Width : 0)
                .ThenBy(item => TryParseSize(item, out var size) ? size.Height : 0)
                .ToArray();

            if (merged.Length != resolutions.Length)
            {
                File.WriteAllLines(resolutionsFilePath, merged);
            }

            return merged.Any() ? merged : defaultResolutions;
        }
        catch
        {
            return defaultResolutions;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = this.GetWindowHandle();
        HwndSource.FromHwnd(_windowHandle)?.AddHook(WindowProc);

        var separationLayerWindow = new Window()
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Title = "Region to Share - Separation Layer",
            ShowInTaskbar = false,
            Top = Top,
            Left = Left,
            Width = 10,
            Height = 10
        };

        separationLayerWindow.MouseDown += SubLayer_MouseDown;
        BindingOperations.SetBinding(separationLayerWindow, BackgroundProperty, new Binding(nameof(SeparationLayerBackground)) { Source = this });

        separationLayerWindow.SourceInitialized += (_, _) =>
        {
            _separationLayerHandle = separationLayerWindow.GetWindowHandle();

            this.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                if (Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Control))
                {
                    var placement = _windowHandle.GetWindowPlacement();

                    placement.NormalPosition.DeserializeFrom(Settings.WindowPlacement);

                    placement.NormalPosition += GlassFrameThickness;
                    _windowHandle.SetWindowPlacement(ref placement);
                    // need to set it twice, if the first call has moved the window to another screen with a different dpi, the size might be incorrect.
                    _windowHandle.SetWindowPlacement(ref placement);
                }

                UpdateSizeAndPos();
                ApplyAnchorNow();

                if (Settings.StartActivated)
                {
                    SetActive();
                }
                else
                {
                    this.BeginInvoke(BringToFront);
                }
            });
        };

        separationLayerWindow.Show();
    }

    private void SetActive()
    {
        OnMouseLeftButtonDown();

        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher.CurrentDispatcher);

        void TimerTick(object sender, EventArgs e)
        {
            if (_recordingWindow != null)
            {
                SendToBack();
            }
            timer.Stop();
        }

        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += TimerTick;
        timer.Start();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        OnMouseLeftButtonDown();
    }

    private void OnMouseLeftButtonDown()
    {
        _debugOffset = Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift) ? new POINT(600, 300) : new POINT();

        if (_recordingWindow != null)
            return;

        InfoArea.Visibility = Visibility.Collapsed;
        RenderTarget.Visibility = Visibility.Visible;

        ValidateSettings();

        _recordingWindow = new RecordingWindow(RenderTarget, Settings.DrawShadowCursor, Settings.FramesPerSecond, _debugOffset, Settings.ShowDesktopWallpaper ? Wallpaper : null);

        NativeWindowRect -= GlassFrameThickness;

        _recordingWindow.SourceInitialized += (_, _) =>
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
        };

        _recordingWindow.Closed += (_, _) =>
        {
            InfoArea.Visibility = Visibility.Visible;
            RenderTarget.Visibility = Visibility.Hidden;
            WindowStyle = WindowStyle.ThreeDBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            _recordingWindow = null;

            NativeWindowRect += GlassFrameThickness;

            UpdateInfoAreaBackground();
            BringToFront();
        };

        _recordingWindow.Show();

        this.BeginInvoke(DispatcherPriority.Background, SendToBack);
    }

    public static bool ValidateSettings()
    {
        try
        {
            var settings = Settings.Default;

            settings.FramesPerSecond = SupportedFramesPerSecond.Contains(settings.FramesPerSecond) ? settings.FramesPerSecond : 15;

            if (!AspectRatio.IsValid(settings.AspectRatio))
            {
                settings.AspectRatio = AspectRatio.Free;
            }

            if (!WindowGeometry.IsValidAnchor(settings.WindowAnchor))
            {
                settings.WindowAnchor = (int)WindowAnchor.None;
            }

            if (!BackgroundColors.IsValid(settings.BackgroundColor))
            {
                settings.BackgroundColor = BackgroundColors.Default;
            }

            if (!ThemeColors.IsValid(settings.ThemeColor))
            {
                settings.ThemeColor = ThemeColors.Default;
            }

            return true;
        }
        catch (ConfigurationException ex)
        {
            var inner = ex.ExceptionChain().OfType<ConfigurationException>().FirstOrDefault(item => !item.Filename.IsNullOrEmpty());
            if (inner == null)
                throw;

            var message = $"The settings file '{inner.Filename}' is corrupt. It will be reset to default values.";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.ServiceNotification);
            File.Delete(inner.Filename);
        }

        return false;
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.ThemeColor):
                SetThemeColor();
                break;

            case nameof(Settings.AspectRatio):
                ApplyAspectRatioNow();
                break;

            case nameof(Settings.WindowAnchor):
                RefreshAnchorButtons();
                ApplyAnchorNow();
                break;

            case nameof(Settings.BackgroundColor):
                ApplyBackgroundColor();
                break;

            case nameof(Settings.ShowDesktopWallpaper):
                Foreground = Settings.ShowDesktopWallpaper ? Brushes.White : BackgroundColors.Get(Settings.BackgroundColor).Foreground;
                IsBackgroundColorEnabled = !Settings.ShowDesktopWallpaper;
                UpdateInfoAreaBackground();
                SetSeparationLayerPos(SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE);
                break;
        }
    }

    private void SetThemeColor()
    {
        Application.Current.Resources["ThemeColor"] = ThemeColors.Get(Settings.ThemeColor).Color;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_windowHandle == IntPtr.Zero)
            return;

        if (e.Property != LeftProperty
            && e.Property != TopProperty
            && e.Property != ActualWidthProperty
            && e.Property != ActualHeightProperty
            && e.Property != WindowStateProperty)
            return;

        UpdateSizeAndPos();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        var normalPosition = _windowHandle.GetWindowPlacement().NormalPosition - GlassFrameThickness;
        Settings.WindowPlacement = normalPosition.Serialize();
        Settings.Save();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _wallpaper?.Dispose();
        _wallpaper = null;
    }

    [Throttled(typeof(DispatcherThrottle), (int)DispatcherPriority.Normal)]
    private void UpdateSizeAndPos()
    {
        if (WindowState == WindowState.Minimized)
            return;

        _recordingWindow?.UpdateSizeAndPos(NativeWindowRect);

        var rect = NativeWindowRect - GlassFrameThickness;
        Extend = rect.Width + "x" + rect.Height;

        SetSeparationLayerPos(SWP_NOACTIVATE | SWP_NOZORDER);

        UpdateInfoAreaBackground();
    }

    private void SubLayer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        this.BeginInvoke(DispatcherPriority.Background, SendToBack);
    }

    public void BringToFront()
    {
        SetSeparationLayerPos(SWP_HIDEWINDOW);
        SetWindowPos(_windowHandle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    public void SendToBack()
    {
        SetSeparationLayerPos(SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowPos(_windowHandle, _separationLayerHandle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Meeting apps sometimes re-order the shared window (issue #45); when the main window is no longer right below
    /// the separation layer it would capture itself and freeze, so send it back again.
    /// </summary>
    public void EnsureSentToBack()
    {
        if (_recordingWindow == null || _separationLayerHandle == IntPtr.Zero || WindowState == WindowState.Minimized)
            return;

        if (NativeMethods.GetWindow(_windowHandle, GW_HWNDPREV) != _separationLayerHandle)
        {
            SendToBack();
        }
    }

    private void SetSeparationLayerPos(uint flags)
    {
        if (_separationLayerHandle == IntPtr.Zero)
            return;

        var rect = NativeWindowRect - _debugOffset;

        SetWindowPos(_separationLayerHandle, HWND_BOTTOM, rect.Left, rect.Top, rect.Width, rect.Height, flags);

        if ((flags & SWP_HIDEWINDOW) == 0)
        {
            SeparationLayerBackground = CreateBackgroundBrush(rect);
        }
    }

    private bool TryParseSize(string value, out SIZE size)
    {
        size = Size.Empty;

        try
        {
            var parts = value.Split('x');

            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                return false;

            size = new SIZE(width, height);

            return size.Width >= MinWidth && size.Height >= MinHeight;
        }
        catch
        {
            return false;
        }
    }
}