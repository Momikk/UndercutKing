using Dalamud.Configuration;

namespace UndercutKing;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ItemHighlight { get; set; } = true;
    public bool RetainerHighlight { get; set; } = true;
    public bool AutomaticPriceCheck { get; set; } = true;
    public int FreshMinutes { get; set; } = 15;
    public List<RetainerSnapshot> Retainers { get; set; } = [];
}
