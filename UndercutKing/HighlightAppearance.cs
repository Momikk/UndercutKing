namespace UndercutKing;

public enum HighlightTheme { Auto, Light, Dark }
public readonly record struct TextColor(byte R, byte G, byte B, byte A = 255)
{
    public double Luminance
    {
        get
        {
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Linear(R) + .7152 * Linear(G) + .0722 * Linear(B);
        }
    }
    public double Contrast(TextColor other) => (Math.Max(Luminance, other.Luminance) + .05) / (Math.Min(Luminance, other.Luminance) + .05);
}

public readonly record struct TextAppearance(TextColor Fill, TextColor Edge, ushort Flags);

public static class HighlightAppearance
{
    // API 15 AtkTextNode.TextFlags.Edge. Keep other text flags (size/alignment are not changed).
    public const ushort EdgeFlag = 1 << 3;

    public static TextAppearance Apply(HighlightStatus status, TextAppearance original, HighlightTheme theme)
    {
        if (status == HighlightStatus.Normal) return original;
        var light = theme == HighlightTheme.Light || theme == HighlightTheme.Auto && original.Fill.Luminance < .35;
        var fill = light
            ? status == HighlightStatus.BagMatch ? new TextColor(0, 55, 125)
                : status == HighlightStatus.Undercut ? new TextColor(120, 20, 20) : new TextColor(90, 55, 0)
            : status == HighlightStatus.BagMatch ? new TextColor(100, 180, 255)
                : status == HighlightStatus.Undercut ? new TextColor(255, 120, 120) : new TextColor(255, 209, 102);
        var edge = light ? new TextColor(245, 245, 245) : new TextColor(16, 16, 16);
        return new(fill, edge, (ushort)(original.Flags | EdgeFlag));
    }

    // A renderer can be recycled or recolored by the game. Restore only fields still owned by us.
    public static TextAppearance Restore(TextAppearance original, TextAppearance current, TextAppearance applied) => new(
        current.Fill == applied.Fill ? original.Fill : current.Fill,
        current.Edge == applied.Edge ? original.Edge : current.Edge,
        (current.Flags & EdgeFlag) == (applied.Flags & EdgeFlag)
            ? (ushort)((current.Flags & ~EdgeFlag) | (original.Flags & EdgeFlag)) : current.Flags);
}
