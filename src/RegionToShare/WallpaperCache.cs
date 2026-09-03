using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
using static RegionToShare.NativeMethods;

namespace RegionToShare;

/// <summary>
/// Renders the desktop wallpaper per monitor exactly as Windows lays it out, and cuts out slices for a screen rectangle.
/// Bitmaps are cached until the wallpaper or the display layout changes. UI thread only.
/// </summary>
public sealed class WallpaperCache : IDisposable
{
    private sealed class MonitorEntry
    {
        public MonitorEntry(RECT monitorRect, WallpaperInfo info, Bitmap bitmap)
        {
            MonitorRect = monitorRect;
            Info = info;
            Bitmap = bitmap;
        }

        public RECT MonitorRect { get; }

        public WallpaperInfo Info { get; }

        public Bitmap Bitmap { get; }
    }

    private readonly Dictionary<IntPtr, MonitorEntry> _monitors = new();
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private Bitmap? _lastSlice;
    private RECT _lastSliceRect;
    private bool _isDisposed;

    public WallpaperCache()
    {
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    /// <summary>
    /// Raised on the UI thread after the cache has been invalidated.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Wallpaper slice for a screen rectangle (physical pixels). The returned bitmap is owned by the cache; do not dispose it.
    /// </summary>
    public Bitmap? GetSlice(RECT nativeRect)
    {
        if (_isDisposed || nativeRect.IsEmpty)
            return null;

        if (_lastSlice != null && _lastSliceRect == nativeRect)
            return _lastSlice;

        var monitorHandle = MonitorFromRect(ref nativeRect, MONITOR_DEFAULTTONEAREST);
        var entry = GetEntry(monitorHandle);

        if (entry == null)
            return null;

        var slice = new Bitmap(nativeRect.Width, nativeRect.Height, PixelFormat.Format32bppPArgb);

        using (var graphics = Graphics.FromImage(slice))
        {
            graphics.Clear(entry.Info.BackgroundColor);

            var source = nativeRect.Intersect(entry.MonitorRect);

            if (!source.IsEmpty)
            {
                graphics.DrawImage(entry.Bitmap,
                    new Rectangle(source.Left - nativeRect.Left, source.Top - nativeRect.Top, source.Width, source.Height),
                    new Rectangle(source.Left - entry.MonitorRect.Left, source.Top - entry.MonitorRect.Top, source.Width, source.Height),
                    GraphicsUnit.Pixel);
            }
        }

        _lastSlice?.Dispose();
        _lastSlice = slice;
        _lastSliceRect = nativeRect;

        return slice;
    }

    /// <summary>
    /// Cheap check for wallpaper changes that are not broadcast (slideshows, Spotlight); invalidates when the path differs.
    /// </summary>
    public void CheckForChanges()
    {
        if (_isDisposed || _monitors.Count == 0)
            return;

        foreach (var entry in _monitors.Values)
        {
            var current = DesktopWallpaper.GetForMonitor(entry.MonitorRect);

            if (current.Path == entry.Info.Path && current.Position == entry.Info.Position)
                continue;

            Invalidate();
            return;
        }
    }

    public void Invalidate()
    {
        if (_isDisposed)
            return;

        Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        Clear();
    }

    private void Clear()
    {
        foreach (var entry in _monitors.Values)
        {
            entry.Bitmap.Dispose();
        }

        _monitors.Clear();
        _lastSlice?.Dispose();
        _lastSlice = null;
    }

    private MonitorEntry? GetEntry(IntPtr monitorHandle)
    {
        if (monitorHandle == IntPtr.Zero)
            return null;

        if (_monitors.TryGetValue(monitorHandle, out var entry))
            return entry;

        var info = MONITORINFO.Default;

        if (!GetMonitorInfo(monitorHandle, ref info) || info.rcMonitor.IsEmpty)
            return null;

        var wallpaper = DesktopWallpaper.GetForMonitor(info.rcMonitor);
        var bitmap = Render(wallpaper, info.rcMonitor);

        entry = new MonitorEntry(info.rcMonitor, wallpaper, bitmap);
        _monitors.Add(monitorHandle, entry);

        return entry;
    }

    private static Bitmap Render(WallpaperInfo wallpaper, RECT monitor)
    {
        var bitmap = new Bitmap(monitor.Width, monitor.Height, PixelFormat.Format32bppPArgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(wallpaper.BackgroundColor);

        using var image = LoadImage(wallpaper.Path);

        if (image == null)
            return bitmap;

        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var imageWidth = image.Width;
        var imageHeight = image.Height;

        switch (wallpaper.Position)
        {
            case WallpaperPosition.Center:
                graphics.DrawImage(image, new Rectangle((monitor.Width - imageWidth) / 2, (monitor.Height - imageHeight) / 2, imageWidth, imageHeight));
                break;

            case WallpaperPosition.Tile:
                // Tiles start at the origin of the primary monitor.
                for (var y = -Modulo(monitor.Top, imageHeight); y < monitor.Height; y += imageHeight)
                {
                    for (var x = -Modulo(monitor.Left, imageWidth); x < monitor.Width; x += imageWidth)
                    {
                        graphics.DrawImage(image, new Rectangle(x, y, imageWidth, imageHeight));
                    }
                }
                break;

            case WallpaperPosition.Stretch:
                graphics.DrawImage(image, new Rectangle(0, 0, monitor.Width, monitor.Height));
                break;

            case WallpaperPosition.Fit:
                DrawScaled(graphics, image, new Rectangle(0, 0, monitor.Width, monitor.Height), Math.Min((double)monitor.Width / imageWidth, (double)monitor.Height / imageHeight));
                break;

            case WallpaperPosition.Span:
                var virtualScreen = new Rectangle(
                    GetSystemMetrics(SM_XVIRTUALSCREEN) - monitor.Left,
                    GetSystemMetrics(SM_YVIRTUALSCREEN) - monitor.Top,
                    GetSystemMetrics(SM_CXVIRTUALSCREEN),
                    GetSystemMetrics(SM_CYVIRTUALSCREEN));
                DrawScaled(graphics, image, virtualScreen, Math.Max((double)virtualScreen.Width / imageWidth, (double)virtualScreen.Height / imageHeight));
                break;

            default: // Fill
                DrawScaled(graphics, image, new Rectangle(0, 0, monitor.Width, monitor.Height), Math.Max((double)monitor.Width / imageWidth, (double)monitor.Height / imageHeight));
                break;
        }

        return bitmap;
    }

    private static void DrawScaled(Graphics graphics, Image image, Rectangle canvas, double scale)
    {
        var width = (int)Math.Round(image.Width * scale);
        var height = (int)Math.Round(image.Height * scale);
        var x = canvas.Left + (canvas.Width - width) / 2;
        var y = canvas.Top + (canvas.Height - height) / 2;

        graphics.DrawImage(image, new Rectangle(x, y, width, height));
    }

    private static Bitmap? LoadImage(string? path)
    {
        var bitmap = TryLoadImage(path);

        if (bitmap != null)
            return bitmap;

        // Windows keeps a JPEG copy of the current wallpaper; covers formats GDI+ can't decode (e.g. HEIC).
        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");

        return TryLoadImage(transcoded);
    }

    private static Bitmap? TryLoadImage(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, false, true);

            // Copy, so the file is not locked for the lifetime of the image.
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private static int Modulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
        {
            _dispatcher.BeginInvoke(Invalidate);
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(Invalidate);
    }
}
