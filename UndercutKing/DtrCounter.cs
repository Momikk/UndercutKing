using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;

namespace UndercutKing;

internal sealed class DtrCounter : IDisposable
{
    private readonly Plugin plugin;
    private readonly IDtrBarEntry entry;
    private UndercutSummary? previous;

    public DtrCounter(Plugin plugin, Action open)
    {
        this.plugin = plugin;
        entry = Plugin.DtrBar.Get("UndercutKing.Undercuts");
        entry.Shown = false;
        entry.OnClick = _ => open();
    }

    public void Update()
    {
        entry.Shown = plugin.Config.ShowDtrCount && Plugin.Player.ContentId != 0;
        if (!entry.Shown) return;
        var summary = UndercutSummary.Count(plugin.Config.Retainers, Plugin.Player.ContentId, plugin.Status);
        if (summary == previous) return;
        previous = summary;
        entry.Text = new SeStringBuilder().AddText(summary.BarText).BuiltString;
        entry.Tooltip = new SeStringBuilder().AddText(
            $"压价之王：已知被压价 {summary.Undercut} 个上架位，价格待刷新 {summary.Unknown} 个。\n" +
            $"当前角色已扫描 {summary.ScannedRetainers} 名雇员；相同物品的不同上架位分别计数。\n" +
            "“+”表示还有待刷新物品，“?”表示数量尚未确认。\n点击打开压价之王。").BuiltString;
    }

    public void Dispose()
    {
        entry.OnClick = null;
        entry.Remove();
    }
}
