using System.Text;

namespace UndercutKing;

public static class HighlightText
{
    // Always compose from the saved original, never from previously decorated text.
    // Normal text retains the game's SeString payloads (icons and colors).
    public static byte[] Compose(byte[] original, string plainText, HighlightStatus status, string suffix)
    {
        var prefix = status == HighlightStatus.Normal ? original.AsSpan(0, original.Length - 1).ToArray()
            : Encoding.UTF8.GetBytes(plainText);
        return [.. prefix, .. Encoding.UTF8.GetBytes(suffix), 0];
    }
}
