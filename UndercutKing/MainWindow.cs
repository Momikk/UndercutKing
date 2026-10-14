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
        var theme = (int)config.HighlightTheme;
        string[] themes = ["自动识别", "浅色界面", "深色界面"];
        if (ImGui.Combo("高亮配色", ref theme, themes, themes.Length))
        {
            config.HighlightTheme = (HighlightTheme)theme;
            plugin.Save();
        }
        var showCount = config.ShowDtrCount;
        if (ImGui.Checkbox("服务器信息栏显示压价数量", ref showCount)) { config.ShowDtrCount = showCount; plugin.Save(); }
        var retainerCount = config.ShowRetainerCount;
        if (ImGui.Checkbox("雇员列表显示各雇员压价数量", ref retainerCount)) { config.ShowRetainerCount = retainerCount; plugin.Save(); }
        ImGui.TextWrapped("雇员市场栏括号内为被压价的上架位数量；(N+) 表示还有价格待确认的商品，(?) 表示数量尚未确认。");
        var item = config.ItemHighlight;
        if (ImGui.Checkbox("高亮雇员销售物品", ref item)) { config.ItemHighlight = item; plugin.Save(); }
        var bagMatch = config.BagMatchHighlight;
        if (ImGui.Checkbox("背包同物品标蓝（不区分 NQ/HQ）", ref bagMatch)) { config.BagMatchHighlight = bagMatch; plugin.Save(); }
        ImGui.TextWrapped("蓝色：普通背包四栏中有同物品，优先于红黄文字颜色；压价数量和下方价格状态仍按市场报价判断。不包含兵装库、陆行鸟鞍囊和雇员背包。");
        var retainers = config.RetainerHighlight;
        if (ImGui.Checkbox("高亮雇员列表", ref retainers)) { config.RetainerHighlight = retainers; plugin.Save(); }
        var automatic = config.AutomaticPriceCheck;
        if (ImGui.Checkbox("自动查询 Universalis 市场价格", ref automatic)) { config.AutomaticPriceCheck = automatic; plugin.Save(); }
        var push = config.UseMarketPush;
        if (ImGui.Checkbox("接收市场变化推送", ref push)) { config.UseMarketPush = push; plugin.Save(); }
        var refreshMinutes = config.AutomaticRefreshMinutes;
        if (ImGui.SliderInt("定期核对间隔（分钟）", ref refreshMinutes, 1, 60))
        {
            config.AutomaticRefreshMinutes = refreshMinutes;
            plugin.Save();
        }
        ImGui.TextWrapped("推送连接正常时按变化补查，默认每 15 分钟批量核对；推送不可用时按不超过 5 分钟的间隔查询。手动刷新同样遵守限流等待。Universalis 依赖玩家上传，仍可能滞后。");
        ImGui.TextWrapped(plugin.Prices.NetworkStatus);
        if (plugin.Prices.PushError.Length != 0) ImGui.TextWrapped(plugin.Prices.PushError);
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
