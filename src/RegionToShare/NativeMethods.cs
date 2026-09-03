// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace RegionToShare;

public static class NativeMethods
{
    public static readonly IntPtr HWND_TOP = new(0);
    public static readonly IntPtr HWND_BOTTOM = new(1);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;

    public const int WM_NCHITTEST = 0x0084;
    public const int WM_SIZING = 0x0214;
    public const int WM_MOVING = 0x0216;
    public const int WM_ENTERSIZEMOVE = 0x0231;
    public const int WM_EXITSIZEMOVE = 0x0232;

    public const int WMSZ_LEFT = 1;
    public const int WMSZ_RIGHT = 2;
    public const int WMSZ_TOP = 3;
    public const int WMSZ_TOPLEFT = 4;
    public const int WMSZ_TOPRIGHT = 5;
    public const int WMSZ_BOTTOM = 6;
    public const int WMSZ_BOTTOMLEFT = 7;
    public const int WMSZ_BOTTOMRIGHT = 8;

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const uint GW_HWNDPREV = 3;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_LAYERED = 0x80000;

    public const int DWMWA_CLOAKED = 14;

    public const uint SPI_GETDESKWALLPAPER = 0x0073;
    public const int COLOR_BACKGROUND = 1;

    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        public static MONITORINFO Default => new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromRect([In] ref RECT lprc, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, System.Text.StringBuilder pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    public static extern uint GetSysColor(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeInt(IntPtr hWnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public static bool IsWindowCloaked(IntPtr hWnd)
    {
        return DwmGetWindowAttributeInt(hWnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    /// <summary>
    /// Visible bounds of a window (without the invisible glass frame); falls back to GetWindowRect.
    /// </summary>
    public static bool TryGetVisibleWindowRect(IntPtr hWnd, out RECT rect)
    {
        rect = new RECT();

        if (DwmGetWindowAttributeRect(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, ref rect, Marshal.SizeOf<RECT>()) == 0)
            return true;

        return GetWindowRect(hWnd, out rect);
    }

    public static bool TryGetMonitorInfo(IntPtr hWnd, out MONITORINFO info)
    {
        info = MONITORINFO.Default;
        var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject([In] IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPlacement(this IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public POINT TopLeft => new() { X = Left, Y = Top };

        public POINT BottomRight => new() { X = Right, Y = Bottom };

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public RECT Intersect(RECT other)
        {
            return new RECT
            {
                Left = Math.Max(Left, other.Left),
                Top = Math.Max(Top, other.Top),
                Right = Math.Min(Right, other.Right),
                Bottom = Math.Min(Bottom, other.Bottom)
            };
        }

        public RECT Offset(int dx, int dy)
        {
            return new RECT { Left = Left + dx, Top = Top + dy, Right = Right + dx, Bottom = Bottom + dy };
        }

        public bool Equals(RECT other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override bool Equals(object? obj)
        {
            return obj is RECT other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Left ^ (Top << 8) ^ (Right << 16) ^ (Bottom << 24);
        }

        public static bool operator ==(RECT a, RECT b) => a.Equals(b);

        public static bool operator !=(RECT a, RECT b) => !a.Equals(b);

        public static RECT operator +(RECT rect, Thickness borderSize)
        {
            return new RECT
            {
                Left = rect.Left - (int)borderSize.Left,
                Top = rect.Top - (int)borderSize.Top,
                Right = rect.Right + (int)borderSize.Right,
                Bottom = rect.Bottom + (int)borderSize.Bottom
            };
        }

        public static RECT operator -(RECT rect, Thickness borderSize)
        {
            return new RECT
            {
                Left = rect.Left + (int)borderSize.Left,
                Top = rect.Top + (int)borderSize.Top,
                Right = rect.Right - (int)borderSize.Right,
                Bottom = rect.Bottom - (int)borderSize.Bottom
            };
        }

        public static RECT operator +(RECT rect, POINT offset)
        {
            return new RECT
            {
                Left = rect.Left + offset.X,
                Top = rect.Top + offset.Y,
                Right = rect.Right + offset.X,
                Bottom = rect.Bottom + offset.Y
            };
        }

        public static RECT operator -(RECT rect, POINT offset)
        {
            return new RECT
            {
                Left = rect.Left - offset.X,
                Top = rect.Top - offset.Y,
                Right = rect.Right - offset.X,
                Bottom = rect.Bottom - offset.Y
            };
        }

        public static implicit operator Rect(RECT r)
        {
            return new Rect(r.TopLeft, r.BottomRight);
        }

        public static implicit operator RECT(Rect r)
        {
            return new RECT { Left = (int)r.Left, Top = (int)r.Top, Right = (int)r.Right, Bottom = (int)r.Bottom };
        }

        public static implicit operator Rectangle(RECT r)
        {
            return new Rectangle(r.Left, r.Top, r.Width, r.Height);
        }

        public static implicit operator RECT(Rectangle r)
        {
            return new RECT { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
        }

        public override string ToString()
        {
            return ((Rect)this).ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static POINT operator +(POINT p1, POINT p2)
        {
            return new POINT { X = p1.X + p2.X, Y = p1.Y + p2.Y };
        }

        public static POINT operator -(POINT p1, POINT p2)
        {
            return new POINT { X = p1.X - p2.X, Y = p1.Y - p2.Y };
        }

        public static implicit operator Point(POINT p)
        {
            return new Point(p.X, p.Y);
        }

        public static implicit operator POINT(Point p)
        {
            return new POINT((int)Math.Round(p.X), (int)Math.Round(p.Y));
        }

        public override string ToString()
        {
            return ((Point)this).ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct SIZE
    {
        public int Width;
        public int Height;

        public SIZE(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static implicit operator Size(SIZE p)
        {
            return new Size(p.Width, p.Height);
        }

        public static implicit operator SIZE(Size p)
        {
            return new SIZE((int)Math.Round(p.Width), (int)Math.Round(p.Height));
        }

        public static SIZE operator +(SIZE size, Thickness thickness)
        {
            return new SIZE(
                size.Width + (int)(thickness.Left + thickness.Right),
                size.Height + (int)(thickness.Top + thickness.Bottom)
            );
        }

        public static SIZE operator -(SIZE size, Thickness thickness)
        {
            return new SIZE(
                size.Width - (int)(thickness.Left + thickness.Right),
                size.Height - (int)(thickness.Top + thickness.Bottom)
            );
        }

        public override string ToString()
        {
            return ((Size)this).ToString();
        }
    }

    public enum HitTest
    {
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Nowhere = 0,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Client = 1,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Caption = 2,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        SysMenu = 3,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        GrowBox = 4,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Size = GrowBox,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Menu = 5,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        HScroll = 6,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        VScroll = 7,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        MinButton = 8,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        MaxButton = 9,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Left = 10,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Right = 11,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Top = 12,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        TopLeft = 13,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        TopRight = 14,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Bottom = 15,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        BottomLeft = 16,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        BottomRight = 17,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Border = 18,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Object = 19,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Close = 20,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Help = 21,
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Error = (-2),
        /// <summary>See documentation of WM_NCHITTEST</summary>
        Transparent = (-1),
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int Length;

        public int Flags;

        public uint ShowCmd;

        public POINT MinPosition;

        public POINT MaxPosition;

        public RECT NormalPosition;

        /// <summary>
        /// Gets the default (empty) value.
        /// </summary>
        public static WINDOWPLACEMENT Default
        {
            get
            {
                var result = new WINDOWPLACEMENT();
                result.Length = Marshal.SizeOf(result);
                return result;
            }
        }
    }

    public const int CURSOR_SHOWING = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorInfo(ref CURSORINFO pci);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeRect(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    public static Thickness DwmGetExtendedFrameBounds(IntPtr hWnd)
    {
        GetWindowRect(hWnd, out var windowRect);
        RECT frameRect = new();

        if (0 != DwmGetWindowAttributeRect(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, ref frameRect, Marshal.SizeOf<RECT>()))
            return new Thickness();
        
        var bounds = new Thickness(
            frameRect.Left - windowRect.Left,
            frameRect.Top - windowRect.Top,
            windowRect.Right - frameRect.Right,
            windowRect.Bottom - frameRect.Bottom
        );

        return bounds;
    }
}