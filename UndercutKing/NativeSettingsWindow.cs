using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
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
    private readonly List<(TextNode Node, HighlightStatus Status)> legend = [];
    private TabBarNode? tabs;
    private readonly List<ResNode> panels = [];
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
        panelWidth = size.X - 12;
        tabs = new TabBarNode { Position = origin, Size = new(size.X, 30) };
        AddOwned(tabs);
        for (var i = 0; i < Pages.Length; i++)
        {
            var page = i;
            tabs.AddTab(Pages[i], () => Select(page));
            var panel = new ResNode { Position = origin + new Vector2(6, 46), Size = new(panelWidth, size.Y - 58) };
            AddOwned(panel);
            panels.Add(panel);
        }
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
        void Apply(bool value)
        {
            if (syncing || read() == value) return;
            write(value);
            Changed();
        }
        var node = new CheckboxNode
        {
            String = "", IsChecked = read(),
            Position = new(0, y), Size = new(26, 26), TextTooltip = hint,
            OnClick = Apply,
        };
        node.AttachNode(panel);
        checks.Add((node, read));
        // The native checkbox can reset its internal label width on initialization.
        // Keep the visible text outside that component's clipping/layout entirely.
        var text = new TextNode
        {
            String = label, Position = new(30, y), Size = new(panelWidth - 32, 28),
            FontSize = 14, AlignmentType = AlignmentType.Left, TextTooltip = hint,
        };
        text.AddEvent(AtkEventType.MouseClick, () =>
        {
            var value = !read();
            node.IsChecked = value;
            Apply(value); // SetChecked may already have fired OnClick; Apply is idempotent.
        });
        text.AttachNode(panel);
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
        ItemCheck(panel, 32);
        MixedCheck(panel, 62);
        DtrCheck(panel, 92);
        Group(panel, "外观", 130);
        Theme(panel, 162);
        (string Label, HighlightStatus Status)[] samples =
            [("红色：被压价", HighlightStatus.Undercut), ("蓝色：同物品", HighlightStatus.BagMatch), ("黄色：待更新", HighlightStatus.Unknown)];
        for (var i = 0; i < samples.Length; i++)
        {
            var node = new TextNode
            {
                String = samples[i].Label, Position = new(i * 148, 200), Size = new(140, 24),
                FontSize = 13, AlignmentType = AlignmentType.Left, UseThemeEmboss = false,
                TextFlags = TextFlags.Edge,
            };
            node.AttachNode(panel);
            legend.Add((node, samples[i].Status));
        }
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
        MixedCheck(panel, 32);
        Check(panel, "与当前雇员在售物品相同", 62, () => plugin.Config.HighlightMatchingBagSlots,
            value => plugin.Config.HighlightMatchingBagSlots = value,
            "打开雇员出售列表时，标出背包中的对应格子；需要开启下方的雇员物品标蓝。");
        Group(panel, "雇员物品标蓝", 102);
        Check(panel, "与背包相同的在售物品", 134, () => plugin.Config.BagMatchHighlight,
            value => plugin.Config.BagMatchHighlight = value,
            "普通背包有相同物品时，在雇员出售列表和雇员总列表中标蓝；不区分 NQ/HQ。");
        Text(panel, "仅检查普通背包四栏，不含兵装库与鞍囊。", 174);
    }
    private void BuildRetainers(ResNode panel)
    {
        Group(panel, "高亮提示", 0);
        ItemCheck(panel, 32);
        Check(panel, "高亮被压价的雇员", 62, () => plugin.Config.RetainerHighlight,
            value => plugin.Config.RetainerHighlight = value,
            "颜色优先级：红色（被压价）> 蓝色（背包同物品）> 黄色（报价待确认）。");
        Group(panel, "压价数量", 102);
        Check(panel, "雇员列表显示压价数量", 134, () => plugin.Config.ShowRetainerCount,
            value => plugin.Config.ShowRetainerCount = value,
            "在每名雇员出售数量后显示压价数量，零不显示。+ 表示还有报价待确认，? 表示数量未知。");
        DtrCheck(panel, 164);
        Theme(panel, 204);
    }
    private void BuildMarket(ResNode panel)
    {
        Group(panel, "自动获取", 0);
        Check(panel, "自动查询市场价格", 32, () => plugin.Config.AutomaticPriceCheck,
            value => plugin.Config.AutomaticPriceCheck = value,
            "使用 Universalis 报价，数据可能滞后；游戏内比较价格得到的完整报价优先使用。");
        Check(panel, "接收市场变化推送", 62, () => plugin.Config.UseMarketPush,
            value => plugin.Config.UseMarketPush = value,
            "变化推送只触发相关商品补查；推送不可用时定期查询，所有请求均遵守限流等待。");
        Number(panel, "定期核对（分钟）", 104, 1, 60, () => plugin.Config.AutomaticRefreshMinutes,
            value => plugin.Config.AutomaticRefreshMinutes = value, () => plugin.Config.AutomaticPriceCheck);
        Text(panel, "报价有效期", 142);
        string[] names = ["1 天", "2 天", "7 天", "自定义（分钟）"];
        int[] durations = [1440, 2880, 10080];
        var preset = Array.IndexOf(durations, plugin.Config.FreshMinutes);
        customFreshness = preset < 0;
        var dropdown = new StringDropDownNode
        {
            Position = new(panelWidth - 160, 142), Size = new(158, 25), Options = names.ToList(),
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
            Position = new(panelWidth - 130, 178), Size = new(126, 28), Min = 1, Max = 43200,
            Step = 1, Value = plugin.Config.FreshMinutes, IsVisible = customFreshness,
            TextTooltip = "自定义有效期，单位分钟，范围 1 分钟至 30 天。",
            OnValueUpdate = value => { if (syncing || plugin.Config.FreshMinutes == value) return; plugin.Config.FreshMinutes = Math.Clamp(value, 1, 43200); Changed(); },
        };
        freshnessInput.AttachNode(panel);
        numbers.Add((freshnessInput, () => plugin.Config.FreshMinutes, () => true));
        new TextButtonNode { String = "刷新报价", Position = new(0, 216), Size = new(112, 28),
            TextTooltip = "刷新已扫描雇员的市场报价，遵守请求队列和限流等待。",
            OnClick = plugin.Refresh }.AttachNode(panel);
        network = Text(panel, "", 254, 58, true);
        dropdown.AttachNode(panel); // popup stays above numeric controls and the refresh button
    }
    private void BuildPerformance(ResNode panel)
    {
        Group(panel, "后台限帧", 0);
        Check(panel, "游戏失去焦点时限帧", 32, () => plugin.Config.LimitBackgroundFps,
            value => plugin.Config.LimitBackgroundFps = value,
            "切回游戏后停止限帧。游戏或显卡设置的更低帧数限制仍然生效。");
        Number(panel, "后台帧数上限", 72, 5, 240, () => plugin.Config.BackgroundFps,
            value => plugin.Config.BackgroundFps = value, () => plugin.Config.LimitBackgroundFps);
        Text(panel, "默认 20 帧，切回游戏后恢复正常运行。", 114);
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
            if (tabs != null)
            {
                tabs.TabButtons[i].IsChecked = i == selected;
                tabs.TabButtons[i].IsSelected = i == selected;
            }
        }
        SyncControls();
        RefreshStatus();
    }
    private void Changed()
    {
        plugin.Save();
        SyncControls();
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
            RefreshLegend();
        }
        finally { syncing = false; }
    }
    private void RefreshLegend()
    {
        // Read an uncolored game text color for Auto, never last frame's sample.
        var baseColor = ColorHelper.GetColor(8);
        var original = new TextAppearance(new((byte)(baseColor.X * 255), (byte)(baseColor.Y * 255), (byte)(baseColor.Z * 255)), default, 0);
        foreach (var (node, status) in legend)
        {
            var style = HighlightAppearance.Apply(status, original, plugin.Config.HighlightTheme);
            var fill = new Vector4(style.Fill.R / 255f, style.Fill.G / 255f, style.Fill.B / 255f, 1);
            var edge = new Vector4(style.Edge.R / 255f, style.Edge.G / 255f, style.Edge.B / 255f, 1);
            if (node.TextColor != fill) node.TextColor = fill;
            if (node.TextOutlineColor != edge) node.TextOutlineColor = edge;
        }
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
        owned.Clear(); panels.Clear(); checks.Clear(); numbers.Clear(); themes.Clear(); legend.Clear();
        network = quoteTitle = null;
        tabs = null;
        prices = null;
        freshnessInput = null;
    }
}
