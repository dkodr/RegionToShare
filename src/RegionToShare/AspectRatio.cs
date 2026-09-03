using System.Globalization;
using System.Windows;
using static RegionToShare.NativeMethods;

namespace RegionToShare;

/// <summary>
/// Aspect ratio lock for the shared region. Width is the master dimension, except when only the top or bottom edge is dragged.
/// </summary>
public static class AspectRatio
{
    public const string Free = "Free";

    public static ICollection<string> Supported { get; } = new[] { Free, "16:9", "3:2", "4:3" };

    public static bool IsValid(string? value)
    {
        return value != null && Supported.Contains(value);
    }

    /// <summary>
    /// Parses "w:h" into width/height; returns false for <see cref="Free"/> or invalid input.
    /// </summary>
    public static bool TryParse(string? value, out double ratio)
    {
        ratio = 0;

        if (value == null || value == Free)
            return false;

        var parts = value.Split(':');

        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || w <= 0 || h <= 0)
            return false;

        ratio = w / h;
        return true;
    }

    /// <summary>
    /// Derives the dependent dimension of the region from the master one; the derived dimension is rounded to even pixels.
    /// </summary>
    public static SIZE Adjust(SIZE region, double ratio, bool heightIsMaster, SIZE minRegion)
    {
        int width, height;

        if (heightIsMaster)
        {
            height = Math.Max(region.Height, minRegion.Height);
            width = RoundEven(height * ratio);

            if (width < minRegion.Width)
            {
                width = minRegion.Width;
                height = RoundEven(width / ratio);
            }
        }
        else
        {
            width = Math.Max(region.Width, minRegion.Width);
            height = RoundEven(width / ratio);

            if (height < minRegion.Height)
            {
                height = minRegion.Height;
                width = RoundEven(height * ratio);
            }
        }

        return new SIZE(width, height);
    }

    /// <summary>
    /// Applies the ratio to a WM_SIZING rectangle: the region is the window rectangle minus <paramref name="nonClient"/>;
    /// the edge being dragged is the one that moves, the opposite edge stays fixed.
    /// </summary>
    public static bool ApplyToSizingRect(ref RECT windowRect, int edge, double ratio, Thickness nonClient, SIZE minRegion)
    {
        var region = windowRect - nonClient;
        var heightIsMaster = edge is WMSZ_TOP or WMSZ_BOTTOM;
        var size = Adjust(new SIZE(region.Width, region.Height), ratio, heightIsMaster, minRegion) + nonClient;

        var result = windowRect;

        if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT)
            result.Left = windowRect.Right - size.Width;
        else
            result.Right = windowRect.Left + size.Width;

        if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT)
            result.Top = windowRect.Bottom - size.Height;
        else
            result.Bottom = windowRect.Top + size.Height;

        if (result == windowRect)
            return false;

        windowRect = result;
        return true;
    }

    private static int RoundEven(double value)
    {
        return (int)Math.Round(value / 2) * 2;
    }
}
