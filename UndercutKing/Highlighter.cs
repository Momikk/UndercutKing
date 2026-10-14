// UI list locations adapted from Critical_Impact/AllaganMarket, AGPL-3.0-or-later.
// Momikk 2026-10-13: retain original text and color per live node; restore on status change and unload.
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace UndercutKing;

internal sealed unsafe class Highlighter : IDisposable
{
    private sealed record NodeState(string Identity, byte[] Original, string PlainText, TextAppearance Appearance, byte[] Applied, TextAppearance AppliedAppearance);
    private readonly Dictionary<nint, NodeState> saleNodes = [];
    private readonly Dictionary<nint, NodeState> retainerNodes = [];
    private readonly Plugin plugin;
    private bool restoreOnly;

    public Highlighter(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Addons.RegisterListener(AddonEvent.PostDraw, "RetainerSellList", DrawSales);
        Plugin.Addons.RegisterListener(AddonEvent.PostDraw, "RetainerList", DrawRetainers);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", FinalizeSales);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, "RetainerList", FinalizeRetainers);
    }

    private static byte[] TextBytes(AtkTextNode* node) => MemoryHelper.ReadRawNullTerminated((nint)(byte*)node->NodeText.StringPtr).Append((byte)0).ToArray();
    private static TextColor ReadColor(ByteColor color) => new(color.R, color.G, color.B, color.A);
    private static ByteColor WriteColor(TextColor color) => new() { R = color.R, G = color.G, B = color.B, A = color.A };
    private static TextAppearance ReadAppearance(AtkTextNode* node) => new(ReadColor(node->TextColor), ReadColor(node->EdgeColor), (ushort)node->TextFlags);
    private static void WriteAppearance(AtkTextNode* node, TextAppearance appearance)
    {
        node->TextColor = WriteColor(appearance.Fill);
        node->EdgeColor = WriteColor(appearance.Edge);
        node->TextFlags = (TextFlags)appearance.Flags;
    }

    private static void Apply(AtkTextNode* node, string identity, HighlightStatus status, Dictionary<nint, NodeState> states, HighlightTheme theme = HighlightTheme.Auto, string suffix = "")
    {
        var key = (nint)node;
        var current = TextBytes(node);
        if (states.TryGetValue(key, out var old))
        {
            // The game may rebuild/recycle this renderer. Only restore text that is still ours.
            if (!current.SequenceEqual(old.Applied) || ReadAppearance(node).Fill != old.AppliedAppearance.Fill)
            {
                // If the game switched theme but the plain text is still ours, restore its markup
                // while preserving the game's newly assigned fill color.
                if (current.SequenceEqual(old.Applied))
                {
                    node->SetText(old.Original.AsSpan());
                    current = old.Original;
                }
                WriteAppearance(node, HighlightAppearance.Restore(old.Appearance, ReadAppearance(node), old.AppliedAppearance));
                states.Remove(key);
            }
            else if (old.Identity != identity || status == HighlightStatus.Normal && suffix.Length == 0)
            {
                node->SetText(old.Original.AsSpan());
                WriteAppearance(node, HighlightAppearance.Restore(old.Appearance, ReadAppearance(node), old.AppliedAppearance));
                states.Remove(key);
                current = old.Original;
            }
        }
        if (status == HighlightStatus.Normal && suffix.Length == 0) return;
        if (!states.TryGetValue(key, out var state))
        {
            var text = node->NodeText.StringPtr.AsDalamudSeString().TextValue;
            var original = current;
            var originalAppearance = ReadAppearance(node);
            state = new(identity, original, text, originalAppearance, current, originalAppearance);
            states[key] = state;
        }
        var currentAppearance = ReadAppearance(node);
        if ((currentAppearance.Flags & ~HighlightAppearance.EdgeFlag) != (state.AppliedAppearance.Flags & ~HighlightAppearance.EdgeFlag))
            state = state with { Appearance = state.Appearance with
            {
                Flags = (ushort)((currentAppearance.Flags & ~HighlightAppearance.EdgeFlag) | (state.Appearance.Flags & HighlightAppearance.EdgeFlag)),
            } };
        var textBytes = HighlightText.Compose(state.Original, state.PlainText, status, suffix);
        if (!current.SequenceEqual(textBytes)) node->SetText(textBytes.AsSpan());
        var desired = status == HighlightStatus.Normal
            ? HighlightAppearance.Restore(state.Appearance, ReadAppearance(node), state.AppliedAppearance)
            : HighlightAppearance.Apply(status, state.Appearance, theme);
        if (ReadAppearance(node) != desired) WriteAppearance(node, desired);
        states[key] = state with { Applied = TextBytes(node), AppliedAppearance = desired };
    }

    private void DrawSales(AddonEvent _, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null) return;
        var list = addon->GetComponentListById(11);
        if (list == null || list->ItemRendererList == null || list->ListLength < 0 || list->ListLength > 20) return;
        var snapshot = plugin.Active;
        var current = NativeReader.ActiveRetainer();
        for (var i = 0; i < list->ListLength; i++)
        {
            var row = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (row == null) continue;
            var node = row->GetTextNodeById(3);
            if (node == null) continue;
            var listing = snapshot != null && snapshot.RetainerId == current
                ? snapshot.Listings.FirstOrDefault(x => x.MenuIndex == row->ListItemIndex) : null;
            var status = restoreOnly ? HighlightStatus.Normal : BagMatchRules.Display(listing,
                listing != null ? plugin.Status(snapshot!, listing) : HighlightStatus.Normal,
                plugin.BagItems, plugin.Config.ItemHighlight, plugin.Config.BagMatchHighlight);
            Apply(node, $"{current}:{listing?.Slot}:{listing?.ItemId}:{listing?.Hq}", status, saleNodes, plugin.Config.HighlightTheme);
        }
    }

    private void DrawRetainers(AddonEvent _, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null) return;
        var list = addon->GetComponentListById(27);
        if (list == null || list->ItemRendererList == null || list->ListLength < 0 || list->ListLength > 10) return;
        var owners = plugin.Reader.Owners();
        for (var i = 0; i < list->ListLength; i++)
        {
            var row = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (row == null) continue;
            var node = row->GetTextNodeById(11);
            if (node == null || row->ListItemIndex < 0 || row->ListItemIndex >= owners.Count) continue;
            var id = owners[row->ListItemIndex].Id;
            var summary = UndercutSummary.Count(plugin.Config.Retainers.Where(x => x.RetainerId == id), Plugin.Player.ContentId, plugin.Status);
            var status = !restoreOnly && plugin.Config.RetainerHighlight
                ? summary.Undercut > 0 ? HighlightStatus.Undercut
                    : summary.Unknown > 0 || summary.ScannedRetainers == 0 ? HighlightStatus.Unknown : HighlightStatus.Normal
                : HighlightStatus.Normal;
            var suffix = !restoreOnly && plugin.Config.ShowRetainerCount ? summary.RetainerSuffix : "";
            Apply(node, $"{Plugin.Player.ContentId}:{id}", status, retainerNodes, plugin.Config.HighlightTheme, suffix);
        }
    }

    private void FinalizeSales(AddonEvent _, AddonArgs args) => saleNodes.Clear();
    private void FinalizeRetainers(AddonEvent _, AddonArgs args) => retainerNodes.Clear();

    public void Restore()
    {
        restoreOnly = true;
        try
        {
            // Enumerate fresh live nodes; never dereference addresses cached in the dictionaries.
            var sell = Plugin.GameGui.GetAddonByName("RetainerSellList");
            if (!sell.IsNull && sell.IsReady) RestoreAddon((AtkUnitBase*)sell.Address, 11, 3, saleNodes);
            var retainers = Plugin.GameGui.GetAddonByName("RetainerList");
            if (!retainers.IsNull && retainers.IsReady) RestoreAddon((AtkUnitBase*)retainers.Address, 27, 11, retainerNodes);
            saleNodes.Clear();
            retainerNodes.Clear();
        }
        finally { restoreOnly = false; }
    }

    private static void RestoreAddon(AtkUnitBase* addon, uint component, uint text, Dictionary<nint, NodeState> states)
    {
        var list = addon->GetComponentListById(component);
        if (list == null || list->ItemRendererList == null) return;
        for (var i = 0; i < list->ListLength; i++)
        {
            var row = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (row == null) continue;
            var node = row->GetTextNodeById(text);
            if (node != null) Apply(node, "restore", HighlightStatus.Normal, states);
        }
    }

    public void Dispose()
    {
        Restore();
        Plugin.Addons.UnregisterListener(AddonEvent.PostDraw, "RetainerSellList", DrawSales);
        Plugin.Addons.UnregisterListener(AddonEvent.PostDraw, "RetainerList", DrawRetainers);
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, "RetainerSellList", FinalizeSales);
        Plugin.Addons.UnregisterListener(AddonEvent.PreFinalize, "RetainerList", FinalizeRetainers);
    }
}
