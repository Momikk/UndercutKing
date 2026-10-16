using Dalamud.Configuration;

namespace UndercutKing;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool ItemHighlight { get; set; } = true;
    public bool BagMatchHighlight { get; set; } = true;
    public bool HighlightMatchingBagSlots { get; set; } = true;
    public bool RetainerHighlight { get; set; } = true;
    public bool ShowDtrCount { get; set; } = true;
    public bool ShowRetainerCount { get; set; } = true;
    public bool AutomaticPriceCheck { get; set; } = true;
    public bool UseMarketPush { get; set; } = true;
    public bool LimitBackgroundFps { get; set; } = true;
    public int BackgroundFps { get; set; } = 20;
    public int AutomaticRefreshMinutes { get; set; } = 15;
    public HighlightTheme HighlightTheme { get; set; } = HighlightTheme.Auto;
    public int FreshMinutes { get; set; } = 2 * 24 * 60;
    public List<RetainerSnapshot> Retainers { get; set; } = [];
}
