using System.Runtime.InteropServices;
using System.Windows;
using RegionToShare.Properties;
using static RegionToShare.NativeMethods;

namespace RegionToShare;

/// <summary>
/// Cell of the 3x3 anchor grid; the visible edge/corner of the window sticks to the work area of its monitor.
/// </summary>
public enum WindowAnchor
{
    None = -1,
    TopLeft = 0,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

/// <summary>
/// Anchor, edge snapping and aspect ratio applied to native window rectangles (physical pixels).
/// </summary>
public static class WindowGeometry
{
    /// <summary>Edge snapping distance in device independent pixels.</summary>
    public const double SnapThreshold = 16;

    public static bool IsValidAnchor(int value)
    {
        return value >= (int)WindowAnchor.None && value <= (int)WindowAnchor.BottomRight;
    }

    public static WindowAnchor CurrentAnchor => IsValidAnchor(Settings.Default.WindowAnchor) ? (WindowAnchor)Settings.Default.WindowAnchor : WindowAnchor.None;

    /// <summary>
    /// Moves the window so its visible rectangle (window minus <paramref name="invisibleFrame"/>) sticks to the anchor; the size is unchanged.
    /// </summary>
    public static RECT ApplyAnchor(RECT windowRect, Thickness invisibleFrame, RECT workArea, WindowAnchor anchor)
    {
        if (anchor == WindowAnchor.None)
            return windowRect;

        var visible = windowRect - invisibleFrame;
        var column = (int)anchor % 3;
        var row = (int)anchor / 3;

        var left = column switch
        {
            0 => workArea.Left,
            1 => workArea.Left + (workArea.Width - visible.Width) / 2,
            _ => workArea.Right - visible.Width
        };

        var top = row switch
        {
            0 => workArea.Top,
            1 => workArea.Top + (workArea.Height - visible.Height) / 2,
            _ => workArea.Bottom - visible.Height
        };

        var moved = new RECT { Left = left, Top = top, Right = left + visible.Width, Bottom = top + visible.Height };

        return moved + invisibleFrame;
    }

    /// <summary>
    /// Snaps the dragged edge(s) of a WM_SIZING rectangle to the work area when within the threshold.
    /// </summary>
    public static RECT SnapToWorkArea(RECT windowRect, int edge, Thickness invisibleFrame, RECT workArea, int threshold)
    {
        var visible = windowRect - invisibleFrame;
        var result = visible;

        if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT)
            result.Left = Snap(visible.Left, workArea.Left, threshold);

        if (edge is WMSZ_RIGHT or WMSZ_TOPRIGHT or WMSZ_BOTTOMRIGHT)
            result.Right = Snap(visible.Right, workArea.Right, threshold);

        if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT)
            result.Top = Snap(visible.Top, workArea.Top, threshold);

        if (edge is WMSZ_BOTTOM or WMSZ_BOTTOMLEFT or WMSZ_BOTTOMRIGHT)
            result.Bottom = Snap(visible.Bottom, workArea.Bottom, threshold);

        return result + invisibleFrame;
    }

    /// <summary>
    /// WM_SIZING handler shared by both windows: edge snapping, then aspect ratio, then anchor. Writes the rectangle back to lParam.
    /// </summary>
    public static IntPtr HandleSizing(IntPtr windowHandle, IntPtr wParam, IntPtr lParam, Thickness invisibleFrame, SIZE minRegion, int snapThreshold)
    {
        var edge = wParam.ToInt32();
        var original = Marshal.PtrToStructure<RECT>(lParam);
        var rect = original;

        var hasMonitor = TryGetMonitorInfo(windowHandle, out var monitor);

        if (hasMonitor)
        {
            rect = SnapToWorkArea(rect, edge, invisibleFrame, monitor.rcWork, snapThreshold);
        }

        if (AspectRatio.TryParse(Settings.Default.AspectRatio, out var ratio))
        {
            AspectRatio.ApplyToSizingRect(ref rect, edge, ratio, invisibleFrame, minRegion);
        }

        if (hasMonitor)
        {
            rect = ApplyAnchor(rect, invisibleFrame, monitor.rcWork, CurrentAnchor);
        }

        if (rect != original)
        {
            Marshal.StructureToPtr(rect, lParam, false);
        }

        return (IntPtr)1;
    }

    private static int Snap(int value, int target, int threshold)
    {
        return Math.Abs(value - target) <= threshold ? target : value;
    }
}
