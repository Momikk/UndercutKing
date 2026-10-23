using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace UndercutKing;

// A transient, non-interactive frame: no inventory node colors, text or mouse handling are modified.
internal sealed unsafe class BagOverlay : IDisposable
{
    private readonly Plugin plugin;
    private DateTimeOffset nextError;
    private HashSet<uint> mixedQualityTargets = [];
    private DateTimeOffset nextQualityScan;
    private ulong qualityCharacter;
    private readonly RecycleKingCompatibility recycleKing = new();
    public BagOverlay(Plugin plugin) { this.plugin = plugin; Plugin.Interface.UiBuilder.Draw += Draw; }

    private static bool Visible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName(name);
        return !addon.IsNull && addon.IsReady && addon.IsVisible;
    }

    private void Draw()
    {
        try { DrawFrames(); }
        catch (Exception ex)
        {
            if (DateTimeOffset.UtcNow < nextError) return;
            nextError = DateTimeOffset.UtcNow.AddSeconds(10);
            Plugin.Log.Error(ex, "Could not draw matching normal-bag slots");
        }
    }

    private void DrawFrames()
    {
        var expanded = Visible("InventoryExpansion");
        var character = Plugin.Player.ContentId;
        if (character == 0 || !expanded && !Visible("Inventory") && !Visible("InventoryLarge"))
        {
            ClearQualityTargets();
            return;
        }
        if (!plugin.Config.HighlightMixedQualities) ClearQualityTargets();
        else if (character != qualityCharacter || DateTimeOffset.UtcNow >= nextQualityScan)
        {
            // Read all four physical bags, even if only one display page is open.
            // Never reuse another character's contents or a partly loaded bag.
            var contents = plugin.Reader.BagContents();
            mixedQualityTargets = contents == null ? [] : MixedQualityRules.Targets(contents);
            qualityCharacter = character;
            nextQualityScan = DateTimeOffset.UtcNow.AddMilliseconds(250);
        }
        var targets = BagOverlayRules.Targets(plugin.Active, Plugin.Player.ContentId, NativeReader.ActiveRetainer(),
            Visible("RetainerSellList"), plugin.Config.BagMatchHighlight, plugin.Config.HighlightMatchingBagSlots, plugin.BagItems);
        targets.UnionWith(mixedQualityTargets);
        BagOverlayRules.ApplyRecyclePriority(targets, recycleKing.Targets());
        if (targets.Count == 0) return;
        var order = ItemOrderModule.Instance();
        var sorter = order == null ? null : order->InventorySorter;
        var manager = InventoryManager.Instance();
        // While the game rebuilds its sorting table there is no trustworthy display-to-source mapping.
        if (manager == null || sorter == null || sorter->SortFunctionIndex != -1 ||
            sorter->InventoryType != InventoryType.Inventory1 || sorter->ItemsPerPage != 35 || sorter->Items.LongCount != 140) return;
        var viewport = ImGui.GetMainViewport().Pos;
        var draw = ImGui.GetForegroundDrawList();
        for (var page = 0; page < 4; page++)
        {
            var addonRef = Plugin.GameGui.GetAddonByName($"InventoryGrid{page}{(expanded ? "E" : "")}");
            if (addonRef.IsNull || !addonRef.IsReady || !addonRef.IsVisible) continue;
            var grid = (AddonInventoryGrid*)addonRef.Address;
            for (var slot = 0; slot < 35; slot++)
            {
                // Items[display index] points to the physical inventory page/slot, including manual sorting.
                var source = sorter->Items[page * 35 + slot].Value;
                if (source == null || !BagOverlayRules.ValidSource(source->Page, source->Slot)) continue;
                var container = manager->GetInventoryContainer((InventoryType)((int)InventoryType.Inventory1 + source->Page));
                if (container == null || !container->IsLoaded || container->Items == null || container->Size != 35) continue;
                var item = container->Items[source->Slot];
                if (item.Quantity <= 0 || !targets.Contains(item.ItemId)) continue;
                var component = grid->Slots[slot].Value;
                if (component == null || component->OwnerNode == null) continue;
                var node = (AtkResNode*)component->OwnerNode;
                if (!NodeVisible(node)) continue;
                var bounds = new Bounds();
                node->GetBounds(&bounds);
                if (bounds.Width is < 12 or > 200 || bounds.Height is < 12 or > 200) continue;
                var min = viewport + new Vector2(bounds.Pos1.X, bounds.Pos1.Y) + Vector2.One;
                var max = viewport + new Vector2(bounds.Pos2.X, bounds.Pos2.Y) - Vector2.One;
                draw.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(.1f, .55f, 1f, .14f)), 4);
                draw.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(.02f, .08f, .2f, .9f)), 4, ImDrawFlags.None, 4);
                draw.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(.25f, .72f, 1f, 1f)), 4, ImDrawFlags.None, 2);
            }
        }
    }

    private void ClearQualityTargets()
    {
        mixedQualityTargets.Clear();
        qualityCharacter = 0;
        nextQualityScan = default;
    }

    private static bool NodeVisible(AtkResNode* node)
    {
        for (var depth = 0; node != null && depth < 32; depth++, node = node->ParentNode)
            if (!node->IsVisible()) return false;
        return node == null;
    }

    public void Dispose() => Plugin.Interface.UiBuilder.Draw -= Draw;
}
