using Dalamud.Configuration;

namespace UndercutKing;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool ItemHighlight { get; set; } = true;
    public bool RetainerHighlight { get; set; } = true;
    public bool ShowDtrCount { get; set; } = true;
    public bool ShowRetainerCount { get; set; } = true;
    public bool AutomaticPriceCheck { get; set; } = true;
    public int AutomaticRefreshMinutes { get; set; } = 5;
    public HighlightTheme HighlightTheme { get; set; } = HighlightTheme.Auto;
    public int FreshMinutes { get; set; } = 2 * 24 * 60;
    public List<RetainerSnapshot> Retainers { get; set; } = [];
}
