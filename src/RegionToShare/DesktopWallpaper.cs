using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using static RegionToShare.NativeMethods;

namespace RegionToShare;

/// <summary>
/// Matches DESKTOP_WALLPAPER_POSITION.
/// </summary>
public enum WallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5
}

public sealed class WallpaperInfo
{
    public WallpaperInfo(string? path, WallpaperPosition position, Color backgroundColor)
    {
        Path = path;
        Position = position;
        BackgroundColor = backgroundColor;
    }

    public string? Path { get; }

    public WallpaperPosition Position { get; }

    public Color BackgroundColor { get; }
}

/// <summary>
/// Reads the current desktop wallpaper via IDesktopWallpaper (per monitor), with a fallback to SystemParametersInfo and the registry.
/// Must be called from an STA thread.
/// </summary>
public static class DesktopWallpaper
{
    // ReSharper disable InconsistentNaming
    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass
    {
    }

    /// <summary>
    /// Member order must match the COM vtable.
    /// </summary>
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();

        void SetPosition(WallpaperPosition position);

        WallpaperPosition GetPosition();

        void SetSlideshow(IntPtr items);

        IntPtr GetSlideshow();

        void SetSlideshowOptions(int options, uint slideshowTick);

        void GetSlideshowOptions(out int options, out uint slideshowTick);

        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, int direction);

        int GetStatus();

        int Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }
    // ReSharper restore InconsistentNaming

    public static WallpaperInfo GetForMonitor(RECT monitorRect)
    {
        try
        {
            return GetFromDesktopWallpaperApi(monitorRect);
        }
        catch
        {
            return GetFromSystemParameters();
        }
    }

    private static WallpaperInfo GetFromDesktopWallpaperApi(RECT monitorRect)
    {
        var api = (IDesktopWallpaper)new DesktopWallpaperClass();

        try
        {
            var count = api.GetMonitorDevicePathCount();
            string? bestId = null;
            var bestDistance = long.MaxValue;

            for (uint i = 0; i < count; i++)
            {
                var id = api.GetMonitorDevicePathAt(i);

                if (string.IsNullOrEmpty(id))
                    continue;

                RECT rect;
                try
                {
                    rect = api.GetMonitorRECT(id);
                }
                catch
                {
                    continue;
                }

                long dx = rect.Left - monitorRect.Left;
                long dy = rect.Top - monitorRect.Top;
                var distance = dx * dx + dy * dy;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestId = id;
                }
            }

            string? path = null;

            try
            {
                path = api.GetWallpaper(bestId);
            }
            catch
            {
                // no wallpaper for this monitor
            }

            var position = api.GetPosition();
            var color = ToColor(api.GetBackgroundColor());

            return new WallpaperInfo(string.IsNullOrEmpty(path) ? null : path, position, color);
        }
        finally
        {
            Marshal.ReleaseComObject(api);
        }
    }

    private static WallpaperInfo GetFromSystemParameters()
    {
        string? path = null;
        var position = WallpaperPosition.Fill;
        var color = Color.Black;

        try
        {
            const int maxPath = 260;
            var buffer = new StringBuilder(maxPath);

            if (SystemParametersInfo(SPI_GETDESKWALLPAPER, maxPath, buffer, 0) && buffer.Length > 0)
            {
                path = buffer.ToString();
            }

            color = ToColor(GetSysColor(COLOR_BACKGROUND));

            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");

            var style = int.TryParse(key?.GetValue("WallpaperStyle") as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 10;
            var tile = key?.GetValue("TileWallpaper") as string == "1";

            position = style switch
            {
                0 => tile ? WallpaperPosition.Tile : WallpaperPosition.Center,
                2 => WallpaperPosition.Stretch,
                6 => WallpaperPosition.Fit,
                22 => WallpaperPosition.Span,
                _ => WallpaperPosition.Fill
            };
        }
        catch
        {
            // keep defaults
        }

        return new WallpaperInfo(path, position, color);
    }

    private static Color ToColor(uint colorRef)
    {
        // COLORREF is 0x00BBGGRR
        return Color.FromArgb((int)(colorRef & 0xFF), (int)((colorRef >> 8) & 0xFF), (int)((colorRef >> 16) & 0xFF));
    }
}
