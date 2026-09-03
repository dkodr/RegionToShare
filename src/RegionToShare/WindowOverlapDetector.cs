using System.Diagnostics;
using System.Text;
using static RegionToShare.NativeMethods;

namespace RegionToShare;

/// <summary>
/// Lists the visible rectangles of all windows above the main window in z-order; the shared region is covered by those,
/// everything else shows the main window itself. Thread safe, cheap enough to be called a few times per second.
/// </summary>
public sealed class WindowOverlapDetector
{
    private readonly IntPtr _mainWindowHandle;
    private readonly uint _ownProcessId = (uint)Process.GetCurrentProcess().Id;

    public WindowOverlapDetector(IntPtr mainWindowHandle)
    {
        _mainWindowHandle = mainWindowHandle;
    }

    public RECT[] Snapshot()
    {
        var result = new List<RECT>();
        var className = new StringBuilder(64);

        EnumWindows((hwnd, _) =>
        {
            // EnumWindows walks from top to bottom; below the main window nothing is visible anyway.
            if (hwnd == _mainWindowHandle)
                return false;

            if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || IsWindowCloaked(hwnd))
                return true;

            GetWindowThreadProcessId(hwnd, out var processId);

            if (processId == _ownProcessId)
                return true;

            // Click-through overlays (e.g. the sharing border drawn by meeting apps) do not hide anything.
            if ((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0)
                return true;

            className.Clear();
            GetClassName(hwnd, className, className.Capacity);
            var name = className.ToString();

            // The desktop itself must not count as a covering window.
            if (name is "Progman" or "WorkerW")
                return true;

            if (TryGetVisibleWindowRect(hwnd, out var rect) && !rect.IsEmpty)
            {
                result.Add(rect);
            }

            return true;
        }, IntPtr.Zero);

        return result.ToArray();
    }
}
