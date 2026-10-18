using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
using Lumina.Excel.Sheets;

namespace UndercutKing;

internal sealed unsafe class NativeSettingsWindow(Plugin plugin) : NativeAddon
{
    private static readonly string[] Pages = ["常用", "背包提示", "雇员提示", "市场数据", "后台限帧", "价格状态"];
    private readonly List<NodeBase> owned = [];
    private readonly List<(CheckboxNode Node, Func<bool> Read)> checks = [];
    private readonly List<(NumericInputNode Node, Func<int> Read, Func<bool> Enabled)> numbers = [];
    private readonly List<StringDropDownNode> themes = [];
    private readonly List<TextButtonNode> navigation = [];
    private readonly List<ResNode> panels = [];
    private TextNode? title;
    private TextNode? help;
    private TextNode? network;
    private TextNode? quoteTitle;
    private ListNode<NativePriceRow, NativePriceRowNode>? prices;
    private int selected;
    private bool syncing;
    private bool pendingOpen;
    private long nextRefresh;
    private float panelWidth;
    private bool customFreshness;
    private NumericInputNode? freshnessInput;

    internal void RequestOpen() { pendingOpen = !IsOpen; TickPendingOpen(); }
    internal void TickPendingOpen()
    {
        if (!pendingOpen || IsAllocated) return;
        pendingOpen = false;
        Open();
    }
    public override void Close() { pendingOpen = false; base.Close(); }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        var origin = ContentStartPosition;
        var size = ContentSize;
        panelWidth = size.X - 160;
        for (var i = 0; i < Pages.Length; i++)
        {
            var page = i;
            var button = new TextButtonNode
            {
                String = Pages[i], Position = origin + new Vector2(0, i * 43), Size = new(130, 34),
                OnClick = () => Select(page),
            };
            AddOwned(button);
            navigation.Add(button);
            var panel = new ResNode { Position = origin + new Vector2(156, 34), Size = new(panelWidth, size.Y - 100) };
            AddOwned(panel);
            panels.Add(panel);
        }
        title = new TextNode { Position = origin + new Vector2(156, 0), Size = new(panelWidth, 28), FontSize = 16 };
        AddOwned(title);
        AddOwned(new HorizontalLineNode { Position = origin + new Vector2(0, size.Y - 62), Size = new(size.X, 2) });
        help = new TextNode
        {
            Position = origin + new Vector2(2, size.Y - 55), Size = new(size.X - 4, 50), FontSize = 12,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
        };
        AddOwned(help);
        BuildCommon(panels[0]);
        BuildBags(panels[1]);
        BuildRetainers(panels[2]);
        BuildMarket(panels[3]);
        BuildPerformance(panels[4]);
        BuildPrices(panels[5]);
        Select(selected);
        SyncControls();
        RefreshStatus();
    }

    private void AddOwned(NodeBase node) { owned.Add(node); AddNode(node); }
    private TextNode Text(ResNode parent, string value, float y, float height = 24, bool wrap = false)
    {
        var node = new TextNode
        {
            String = value, Position = new(0, y), Size = new(panelWidth, height), FontSize = 13,
            AlignmentType = AlignmentType.Left,
            TextFlags = wrap ? TextFlags.WordWrap | TextFlags.MultiLine : TextFlags.Ellipsis,
            TextTooltip = value,
        };
        node.AttachNode(parent);
        return node;
    }
    private void Group(ResNode panel, string label, float y)
    {
        Text(panel, label, y);
        new HorizontalLineNode { Position = new(0, y + 24), Size = new(panelWidth, 2) }.AttachNode(panel);
    }
    private void Check(ResNode panel, string label, float y, Func<bool> read, Action<bool> write, string hint)
    {
        var node = new CheckboxNode
        {
            String = label, IsChecked = read(), DisableAutoResize = true,
            Position = new(0, y), Size = new(panelWidth - 38, 26), TextTooltip = hint,
            OnClick = value => { if (syncing || read() == value) return; write(value); Changed(); },
        };
        node.AttachNode(panel);
        checks.Add((node, read));
        new TextButtonNode
        {
            String = "?", Position = new(panelWidth - 28, y), Size = new(26, 26), TextTooltip = hint,
            OnClick = () => { if (help != null) help.String = hint; },
        }.AttachNode(panel);
    }
    private void Number(ResNode panel, string label, float y, int min, int max, Func<int> read, Action<int> write, Func<bool>? enabled = null)
    {
        Text(panel, label, y + 2);
        var node = new NumericInputNode
        {
            Position = new(panelWidth - 130, y), Size = new(126, 28), Min = min, Max = max, Step = 1, Value = read(),
            OnValueUpdate = value => { if (syncing || read() == value) return; write(Math.Clamp(value, min, max)); Changed(); },
        };
        node.AttachNode(panel);
        numbers.Add((node, read, enabled ?? (() => true)));
    }
    private void Theme(ResNode panel, float y)
    {
        string[] names = ["自动识别", "浅色界面", "深色界面"];
        Text(panel, "高亮配色", y);
        var dropdown = new StringDropDownNode
        {
            Position = new(panelWidth - 150, y), Size = new(148, 25), Options = names.ToList(),
            SelectedOption = names[(int)plugin.Config.HighlightTheme], MaxListOptions = 3,
            OnOptionSelected = name =>
            {
                if (syncing) return;
                plugin.Config.HighlightTheme = (HighlightTheme)Array.IndexOf(names, name);
                Changed();
            },
        };
        dropdown.AttachNode(panel);
        themes.Add(dropdown);
    }
    private void BuildCommon(ResNode panel)
    {
        Group(panel, "常用提示", 0);
        ItemCheck(panel, 38);
        MixedCheck(panel, 78);
        DtrCheck(panel, 118);
        Group(panel, "外观", 166);
        Theme(panel, 204);
        Text(panel, "红色：被压价    蓝色：同物品    黄色：待更新", 252);
    }
    private void ItemCheck(ResNode panel, float y) => Check(panel, "高亮被压价的物品", y,
        () => plugin.Config.ItemHighlight, value => plugin.Config.ItemHighlight = value,
        "红色表示售价高于市场最低价，黄色表示报价未知或过期。NQ/HQ 共用最低价，仅提示，不自动改价。");
    private void MixedCheck(ResNode panel, float y) => Check(panel, "同物品同时有 NQ 和 HQ", y,
        () => plugin.Config.HighlightMixedQualities, value => plugin.Config.HighlightMixedQualities = value,
        "普通背包里同物品同时有 NQ 和 HQ 时，用蓝框标出两种品质的所有格子，无需召唤雇员。");
    private void DtrCheck(ResNode panel, float y) => Check(panel, "服务器信息栏显示压价数量", y,
        () => plugin.Config.ShowDtrCount, value => plugin.Config.ShowDtrCount = value,
        "统计当前角色所有已扫描雇员被压价的上架位。点击服务器信息栏的压价数量可打开本窗口。");
    private void BuildBags(ResNode panel)
    {
        Group(panel, "普通背包蓝框", 0);
        MixedCheck(panel, 38);
        Check(panel, "与当前雇员在售物品相同", 78, () => plugin.Config.HighlightMatchingBagSlots,
            value => plugin.Config.HighlightMatchingBagSlots = value,
            "打开雇员出售列表时，标出背包中的对应格子；需要开启下方的雇员物品标蓝。");
        Group(panel, "雇员物品标蓝", 128);
        Check(panel, "与背包相同的在售物品", 166, () => plugin.Config.BagMatchHighlight,
            value => plugin.Config.BagMatchHighlight = value,
            "普通背包有相同物品时，在雇员出售列表和雇员总列表中标蓝；不区分 NQ/HQ。");
        Text(panel, "仅检查普通背包四栏，不含兵装库与鞍囊。", 222);
    }
    private void BuildRetainers(ResNode panel)
    {
        Group(panel, "高亮提示", 0);
        ItemCheck(panel, 38);
        Check(panel, "高亮被压价的雇员", 78, () => plugin.Config.RetainerHighlight,
            value => plugin.Config.RetainerHighlight = value,
            "颜色优先级：红色（被压价）> 蓝色（背包同物品）> 黄色（报价待确认）。");
        Group(panel, "压价数量", 128);
        Check(panel, "雇员列表显示压价数量", 166, () => plugin.Config.ShowRetainerCount,
            value => plugin.Config.ShowRetainerCount = value,
            "在每名雇员出售数量后显示压价数量，零不显示。+ 表示还有报价待确认，? 表示数量未知。");
        DtrCheck(panel, 206);
        Theme(panel, 256);
    }
    private void BuildMarket(ResNode panel)
    {
        Group(panel, "自动获取", 0);
        Check(panel, "自动查询市场价格", 38, () => plugin.Config.AutomaticPriceCheck,
            value => plugin.Config.AutomaticPriceCheck = value,
            "使用 Universalis 报价，数据可能滞后；游戏内比较价格得到的完整报价优先使用。");
        Check(panel, "接收市场变化推送", 78, () => plugin.Config.UseMarketPush,
            value => plugin.Config.UseMarketPush = value,
            "变化推送只触发相关商品补查；推送不可用时定期查询，所有请求均遵守限流等待。");
        Number(panel, "定期核对（分钟）", 126, 1, 60, () => plugin.Config.AutomaticRefreshMinutes,
            value => plugin.Config.AutomaticRefreshMinutes = value, () => plugin.Config.AutomaticPriceCheck);
        Text(panel, "报价有效期", 166);
        string[] names = ["1 天", "2 天", "7 天", "自定义（分钟）"];
        int[] durations = [1440, 2880, 10080];
        var preset = Array.IndexOf(durations, plugin.Config.FreshMinutes);
        customFreshness = preset < 0;
        var dropdown = new StringDropDownNode
        {
            Position = new(panelWidth - 160, 166), Size = new(158, 25), Options = names.ToList(),
            SelectedOption = names[preset < 0 ? 3 : preset], MaxListOptions = 4,
            OnOptionSelected = name =>
            {
                var index = Array.IndexOf(names, name);
                customFreshness = index == 3;
                if (freshnessInput != null) freshnessInput.IsVisible = customFreshness;
                if (index is >= 0 and < 3) { plugin.Config.FreshMinutes = durations[index]; Changed(); }
            },
        };
        freshnessInput = new NumericInputNode
        {
            Position = new(panelWidth - 130, 204), Size = new(126, 28), Min = 1, Max = 43200,
            Step = 1, Value = plugin.Config.FreshMinutes, IsVisible = customFreshness,
            TextTooltip = "自定义有效期，单位分钟，范围 1 分钟至 30 天。",
            OnValueUpdate = value => { if (syncing || plugin.Config.FreshMinutes == value) return; plugin.Config.FreshMinutes = Math.Clamp(value, 1, 43200); Changed(); },
        };
        freshnessInput.AttachNode(panel);
        numbers.Add((freshnessInput, () => plugin.Config.FreshMinutes, () => true));
        new TextButtonNode { String = "刷新报价", Position = new(0, 244), Size = new(112, 28),
            TextTooltip = "刷新已扫描雇员的市场报价，遵守请求队列和限流等待。",
            OnClick = () => { plugin.Refresh(); if (help != null) help.String = "已提交报价刷新，遵守限流等待。"; } }.AttachNode(panel);
        network = Text(panel, "", 286, 58, true);
        dropdown.AttachNode(panel); // popup stays above numeric controls and the refresh button
    }
    private void BuildPerformance(ResNode panel)
    {
        Group(panel, "后台限帧", 0);
        Check(panel, "游戏失去焦点时限帧", 38, () => plugin.Config.LimitBackgroundFps,
            value => plugin.Config.LimitBackgroundFps = value,
            "切回游戏后停止限帧。游戏或显卡设置的更低帧数限制仍然生效。");
        Number(panel, "后台帧数上限", 88, 5, 240, () => plugin.Config.BackgroundFps,
            value => plugin.Config.BackgroundFps = value, () => plugin.Config.LimitBackgroundFps);
        Text(panel, "默认 20 帧，切回游戏后恢复正常运行。", 138);
    }
    private void BuildPrices(ResNode panel)
    {
        quoteTitle = Text(panel, "打开雇员出售列表以查看当前物品。", 0);
        string[] headers = ["物品", "我的单价", "最低价", "状态"];
        for (var i = 0; i < headers.Length; i++)
        {
            var (x, width) = NativePriceRowNode.Column(i, panelWidth - 24);
            new TextNode { String = headers[i], Position = new(x, 34), Size = new(width, 24), FontSize = 13 }.AttachNode(panel);
        }
        prices = new ListNode<NativePriceRow, NativePriceRowNode>
        {
            OptionsList = [], AutoResetScroll = false, ItemSpacing = 0,
            Position = new(0, 64), Size = new(panelWidth, panel.Height - 70),
        };
        prices.NoResultsTextNode.TextId = 0;
        prices.NoResultsTextNode.String = "打开雇员出售列表以查看当前物品。";
        prices.AttachNode(panel);
    }
    private void Select(int page)
    {
        selected = Math.Clamp(page, 0, Pages.Length - 1);
        for (var i = 0; i < panels.Count; i++)
        {
            panels[i].IsVisible = i == selected;
            navigation[i].String = (i == selected ? "▶ " : "") + Pages[i];
        }
        if (title != null) title.String = Pages[selected];
        if (help != null) help.String = selected == 3 ? "默认每 15 分钟核对，报价有效期为两天。自定义单位为分钟。"
            : "更改自动保存。悬停或点击 ? 查看说明。";
        SyncControls();
        RefreshStatus();
    }
    private void Changed()
    {
        plugin.Save();
        SyncControls();
        if (help != null) help.String = "设置已保存。";
    }
    private void SyncControls()
    {
        syncing = true;
        try
        {
            foreach (var (node, read) in checks) if (node.IsChecked != read()) node.IsChecked = read();
            foreach (var (node, read, enabled) in numbers)
            {
                if (node.Value != read()) node.Value = read();
                node.IsEnabled = enabled();
            }
            string[] names = ["自动识别", "浅色界面", "深色界面"];
            foreach (var node in themes) if (node.SelectedOption != names[(int)plugin.Config.HighlightTheme]) node.SelectedOption = names[(int)plugin.Config.HighlightTheme];
        }
        finally { syncing = false; }
    }
    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (!IsOpen || Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 500;
        SyncControls();
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        if (network != null && selected == 3)
        {
            var state = string.Join("\n", new[] { plugin.Prices.NetworkStatus,
                plugin.Prices.PushError, plugin.Prices.LastError, plugin.Reader.Error }.Where(x => x.Length != 0));
            network.String = state;
            network.TextTooltip = state;
        }
        if (prices == null || quoteTitle == null || selected != 5) return;
        var active = plugin.Active;
        quoteTitle.String = active == null ? "打开雇员出售列表以查看当前物品。" : $"{active.Name} · {active.Listings.Count} 件在售物品";
        var rows = active == null ? [] : active.Listings.OrderBy(x => x.MenuIndex).Select(listing =>
        {
            var quote = plugin.Prices.Get(active.WorldId, listing.ItemId);
            var name = Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(listing.ItemId)?.Name.ToString() ?? listing.ItemId.ToString();
            var status = plugin.Status(active, listing);
            return new NativePriceRow(name + (listing.Hq ? " HQ" : ""), listing.Price.ToString("N0"), quote?.Lowest?.ToString("N0") ?? "—",
                status == HighlightStatus.Undercut ? "被压价" : status == HighlightStatus.Unknown ? "待刷新" : "正常", quote?.Source ?? "未知");
        }).ToList();
        if (!prices.OptionsList.SequenceEqual(rows)) prices.OptionsList = rows;
    }
    protected override void OnFinalize(AtkUnitBase* addon)
    {
        // Dispose descendants while their native parents are still alive. All
        // managed bindings are then cleared before the next allocation.
        foreach (var node in owned) node.Dispose();
        owned.Clear(); panels.Clear(); navigation.Clear(); checks.Clear(); numbers.Clear(); themes.Clear();
        title = help = network = quoteTitle = null;
        prices = null;
        freshnessInput = null;
    }
}
