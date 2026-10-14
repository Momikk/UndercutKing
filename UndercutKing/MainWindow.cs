using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace UndercutKing;

internal sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    public MainWindow(Plugin plugin) : base("压价之王 UndercutKing###UndercutKingMain")
    {
        this.plugin = plugin;
        Size = new Vector2(640, 410);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("红色：售价高于同物品的市场最低价，不区分 NQ/HQ；黄色：市场数据未知或已过期。改价后自动重新读取售价。仅提示，不自动改价。");
        var config = plugin.Config;
        var showCount = config.ShowDtrCount;
        if (ImGui.Checkbox("服务器信息栏显示压价数量", ref showCount)) { config.ShowDtrCount = showCount; plugin.Save(); }
        var item = config.ItemHighlight;
        if (ImGui.Checkbox("高亮雇员销售物品", ref item)) { config.ItemHighlight = item; plugin.Save(); }
        var retainers = config.RetainerHighlight;
        if (ImGui.Checkbox("高亮雇员列表", ref retainers)) { config.RetainerHighlight = retainers; plugin.Save(); }
        var automatic = config.AutomaticPriceCheck;
        if (ImGui.Checkbox("自动查询 Universalis 市场价格", ref automatic)) { config.AutomaticPriceCheck = automatic; plugin.Save(); }
        var days = config.FreshMinutes / (24f * 60f);
        if (ImGui.SliderFloat("价格有效期（天）", ref days, 1f / (24 * 60), 30, "%.2f"))
        {
            config.FreshMinutes = Math.Clamp((int)Math.Round(days * 24 * 60), 1, 30 * 24 * 60);
            plugin.Save();
        }
        if (ImGui.Button("设为两天")) { config.FreshMinutes = 2 * 24 * 60; plugin.Save(); }
        if (ImGui.Button("刷新已扫描雇员的市场价格")) plugin.Refresh();
        ImGui.TextWrapped("首次使用请依次打开各雇员的销售列表。Universalis 数据可能滞后；游戏内比较价格得到的完整列表优先使用。");
        if (plugin.Reader.Error.Length != 0) ImGui.TextColored(new Vector4(1, .35f, .35f, 1), plugin.Reader.Error);
        if (plugin.Prices.LastError.Length != 0) ImGui.TextWrapped(plugin.Prices.LastError);
        ImGui.Separator();
        var active = plugin.Active;
        if (active == null) { ImGui.TextUnformatted("打开雇员销售列表以查看当前物品。加载插件时窗口已打开的，请关闭后重新召唤雇员。"); return; }
        ImGui.TextUnformatted($"当前雇员：{active.Name} · {active.Listings.Count} 件上架物品");
        if (!ImGui.BeginTable("Listings", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) return;
        try
        {
            ImGui.TableSetupColumn("物品"); ImGui.TableSetupColumn("我的单价"); ImGui.TableSetupColumn("市场最低价"); ImGui.TableSetupColumn("状态 / 来源");
            ImGui.TableHeadersRow();
            foreach (var listing in active.Listings.OrderBy(x => x.MenuIndex))
            {
                var quote = plugin.Prices.Get(active.WorldId, listing.ItemId);
                var status = plugin.Status(active, listing);
                var name = Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(listing.ItemId)?.Name.ToString() ?? listing.ItemId.ToString();
                ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted(name + (listing.Hq ? " HQ" : ""));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(listing.Price.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(quote?.Lowest?.ToString("N0") ?? "暂无报价");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(status == HighlightStatus.Undercut ? "被压价" : status == HighlightStatus.Unknown ? "待刷新" : "正常")} / {quote?.Source ?? "未知"}");
            }
        }
        finally { ImGui.EndTable(); }
    }
}
