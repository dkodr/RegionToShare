using System.Windows.Media;

namespace RegionToShare;

public sealed class NamedColor
{
    public NamedColor(string name, string hex)
    {
        Name = name;
        Color = (Color)ColorConverter.ConvertFromString(hex);
        Brush = new SolidColorBrush(Color);
        Brush.Freeze();
    }

    public string Name { get; }

    public Color Color { get; }

    public Brush Brush { get; }

    /// <summary>
    /// Text color that stays readable on this background.
    /// </summary>
    public Brush Foreground => IsLight ? Brushes.Black : Brushes.White;

    public bool IsLight => (0.299 * Color.R + 0.587 * Color.G + 0.114 * Color.B) > 140;

    public override string ToString() => Name;
}

/// <summary>
/// Solid background colors offered in the settings; the first one is the default.
/// </summary>
public static class BackgroundColors
{
    public const string Default = "Black";

    public static IReadOnlyList<NamedColor> Items { get; } = new[]
    {
        new NamedColor("Black", "#000000"),
        new NamedColor("Charcoal", "#2B2B2B"),
        new NamedColor("Slate", "#3A4750"),
        new NamedColor("Navy", "#1F2A44"),
        new NamedColor("Mist", "#D9DEE3"),
        new NamedColor("Cream", "#FFF5E1"),
        new NamedColor("Sand", "#EAD7BB"),
        new NamedColor("Butter", "#FFF1B5"),
        new NamedColor("Peach", "#FFD8B1"),
        new NamedColor("Blush", "#F8C8DC"),
        new NamedColor("Rose", "#F4B6C2"),
        new NamedColor("Lilac", "#E4C1F9"),
        new NamedColor("Lavender", "#D9CCEF"),
        new NamedColor("Powder blue", "#B5D0E8"),
        new NamedColor("Sky", "#BFE3F7"),
        new NamedColor("Seafoam", "#A8E6CF"),
        new NamedColor("Mint", "#BDECD8"),
        new NamedColor("Sage", "#C9D8C5"),
    };

    public static bool IsValid(string? name)
    {
        return name != null && Items.Any(item => item.Name == name);
    }

    public static NamedColor Get(string? name)
    {
        return Items.FirstOrDefault(item => item.Name == name) ?? Items[0];
    }
}

/// <summary>
/// Theme (accent) colors: frame stripes and UI accents. Five vivid, clearly distinct colors; the first one is the default.
/// </summary>
public static class ThemeColors
{
    public const string Default = "Steel blue";

    public static IReadOnlyList<NamedColor> Items { get; } = new[]
    {
        new NamedColor("Steel blue", "#4682B4"),
        new NamedColor("Crimson", "#DC143C"),
        new NamedColor("Orange", "#FF8C00"),
        new NamedColor("Emerald", "#2ECC71"),
        new NamedColor("Violet", "#8E44AD"),
    };

    public static bool IsValid(string? name)
    {
        return name != null && Items.Any(item => item.Name == name);
    }

    public static NamedColor Get(string? name)
    {
        return Items.FirstOrDefault(item => item.Name == name) ?? Items[0];
    }
}
