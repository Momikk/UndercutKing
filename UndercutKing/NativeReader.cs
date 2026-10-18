// Inventory/network signatures and UI ordering adapted from Critical_Impact/AllaganMarket.
// Modified by Momikk, 2026-10-13, under AGPL-3.0-or-later. See NOTICE.md.
using System.Runtime.InteropServices;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace UndercutKing;

internal sealed unsafe class NativeReader : IDisposable
{
    internal const string InventorySignature = "48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 44 0F B7 42 ??";
    internal const string MarketSignature = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 48 8B DA E8 ?? ?? ?? ??";
    [Signature(InventorySignature, DetourName = nameof(ContainerInfo), UseFlags = SignatureUseFlags.Hook)]
    private Hook<ContainerPacket>? inventoryHook = null;
    [Signature(MarketSignature, DetourName = nameof(MarketRequest), UseFlags = SignatureUseFlags.Hook)]
    private Hook<MarketPacket>? marketHook = null;
    private delegate void* ContainerPacket(int sequence, int* packet);
    private delegate void MarketPacket(uint sequence, nint packet);
    private readonly MarketPrices prices;
    private readonly RetainerLoadState loadState = new();
    public bool InventoryHookReady => inventoryHook?.IsEnabled == true;
    public bool MarketHookReady => marketHook?.IsEnabled == true;
    public string Error { get; private set; } = "";
    internal event Action<RetainerSnapshot>? SnapshotReady;

    public NativeReader(MarketPrices prices)
    {
        this.prices = prices;
        try
        {
            Plugin.Interop.InitializeFromAttributes(this);
            inventoryHook?.Enable();
            marketHook?.Enable();
            if (!InventoryHookReady) Error = "库存签名不可用，请等待适配当前游戏版本。";
        }
        catch (Exception ex)
        {
            Error = "游戏交互初始化失败，请查看 /xllog。";
            Plugin.Log.Error(ex, "UndercutKing native hooks initialization failed");
            inventoryHook?.Disable();
            marketHook?.Disable();
        }
        foreach (var type in MenuEvents) Plugin.Addons.RegisterListener(type, "SelectString", MenuChanged);
    }

    private static readonly AddonEvent[] MenuEvents =
        [AddonEvent.PostShow, AddonEvent.PreHide, AddonEvent.PreFinalize];

    private void MenuChanged(AddonEvent type, AddonArgs args)
    {
        try
        {
            var id = ActiveRetainer();
            if (id == 0 || Plugin.Player.ContentId == 0) return;
            loadState.MenuReady(id);
            // Before hide/finalize, the menu may already report invisible. The
            // callback itself supplies the current interaction boundary.
            var snapshot = Capture(Plugin.Player.ContentId, true);
            if (snapshot != null)
            {
                SnapshotReady?.Invoke(snapshot);
                Plugin.Log.Debug("Retainer snapshot refreshed at {Event}: {Retainer}, {Slots} slots", type, id, snapshot.Listings.Count);
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Could not refresh retainer snapshot at menu transition"); }
    }

    public static ulong ActiveRetainer()
    {
        var framework = GameFramework.Instance();
        if (framework == null || framework->UIModule == null) return 0;
        var order = framework->UIModule->GetItemOrderModule();
        return order == null ? 0 : order->ActiveRetainerId;
    }

    private void* ContainerInfo(int sequence, int* packet)
    {
        // Read the packet while its caller still owns the buffer, then let the game finish
        // receiving it before permitting a snapshot. Call Original exactly once.
        var retainer = 0UL;
        try
        {
            loadState.Observe(ActiveRetainer());
            if (packet != null)
            {
                var payload = (byte*)packet + 16;
                var container = *(uint*)(payload + 8);
                if (container == (uint)InventoryType.RetainerMarket)
                    retainer = ActiveRetainer();
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Could not observe retainer inventory packet"); }
        var result = inventoryHook!.Original(sequence, packet);
        if (retainer != 0 && retainer == ActiveRetainer()) loadState.Received(retainer);
        return result;
    }

    private void MarketRequest(uint sequence, nint packet)
    {
        try
        {
            if (packet != 0 && Plugin.Objects.LocalPlayer is { } player)
            {
                var manager = InventoryManager.Instance();
                var selected = manager == null || ActiveRetainer() == 0 ? null : manager->GetInventorySlot(InventoryType.BlockedItems, 0);
                prices.Begin((uint)Marshal.ReadInt32(packet), (uint)Marshal.ReadInt32(packet, 4),
                    player.CurrentWorld.RowId, selected == null ? 0 : selected->ItemId);
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Could not observe market request"); }
        marketHook!.OriginalDisposeSafe(sequence, packet);
    }

    public List<(ulong Id, string Name)> Owners()
    {
        var owners = new List<(ulong, string)>();
        var manager = RetainerManager.Instance();
        if (manager == null || !manager->IsReady) return owners;
        for (uint i = 0; i < Math.Min(manager->GetRetainerCount(), (byte)10); i++)
        {
            var retainer = manager->GetRetainerBySortedIndex(i);
            if (retainer != null && retainer->RetainerId != 0)
                owners.Add((retainer->RetainerId, retainer->NameString.Trim()));
        }
        return owners;
    }

    public HashSet<uint>? BagItems()
    {
        var manager = InventoryManager.Instance();
        if (manager == null || Plugin.Objects.LocalPlayer == null) return null;
        var slots = new List<(uint ItemId, uint Quantity)>();
        foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
        {
            var container = manager->GetInventoryContainer(type);
            // Require all four normal bags to be loaded; never reuse another character's bag snapshot.
            if (container == null || !container->IsLoaded || container->Items == null || container->Size is < 1 or > 140) return null;
            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->Items[slot];
                if (item.ItemId != 0 && item.Quantity > 0) slots.Add((item.ItemId, (uint)item.Quantity));
            }
        }
        return BagMatchRules.ItemIds(slots);
    }

    public static bool RetainerListVisible()
    {
        var addon = Plugin.GameGui.GetAddonByName("RetainerList");
        return !addon.IsNull && addon.IsReady && addon.IsVisible;
    }

    public RetainerSnapshot? Capture(ulong characterId, bool menuTransition = false)
    {
        var id = ActiveRetainer();
        // Also retry while the menu is visible if its PostShow preceded the ID
        // or a price becoming available. Do not depend on the overview's count.
        var menuVisible = Visible("SelectString");
        if (menuVisible) loadState.MenuReady(id);
        // A retainer change invalidates loading even if the old container still looks populated.
        var ready = loadState.Ready(id);
        if (!InventoryHookReady || !ready || Plugin.Objects.LocalPlayer is not { } player) return null;
        var saleVisible = Visible("RetainerSellList");
        if (!saleVisible && !menuVisible && !menuTransition) return null;
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        var container = manager->GetInventoryContainer(InventoryType.RetainerMarket);
        if (container == null || !container->IsLoaded || container->Items == null || container->Size != 20) return null;
        var result = new RetainerSnapshot
        {
            CharacterId = characterId, RetainerId = id, WorldId = player.HomeWorld.RowId,
            Name = Owners().FirstOrDefault(x => x.Id == id).Name ?? "雇员",
            CapturedAt = DateTimeOffset.UtcNow,
        };
        for (short slot = 0; slot < 20; slot++)
        {
            var item = container->Items[slot];
            if (item.ItemId == 0) continue;
            if (item.Quantity <= 0) return null;
            var price = manager->GetRetainerMarketPrice(slot);
            if (price == 0 || price > uint.MaxValue) return null; // wait for the price packet, never replace a known price with zero
            result.Listings.Add(new(slot, -1, item.ItemId, (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0,
                (uint)item.Quantity, (uint)price));
        }
        if (saleVisible && ForSaleList(result) == null) return null; // incomplete UI rebuild
        return result;
    }

    private static bool Visible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName(name);
        return !addon.IsNull && addon.IsReady && addon.IsVisible;
    }

    public RetainerSnapshot? ForSaleList(RetainerSnapshot? snapshot)
    {
        if (snapshot == null || snapshot.RetainerId != ActiveRetainer()) return null;
        var addonRef = Plugin.GameGui.GetAddonByName("RetainerSellList");
        if (addonRef.IsNull || !addonRef.IsReady || !addonRef.IsVisible) return null;
        var addon = (AtkUnitBase*)addonRef.Address;
        if (addon->AtkValues == null || addon->AtkValuesCount < 263) return null;
        var order = new Dictionary<int, int>();
        for (var i = 0; i < 20; i++)
        {
            var value = addon->AtkValues[15 + i * 13];
            if (value.Type == 0 || value.Int < 0 || value.Int >= 20) continue;
            order.TryAdd(value.Int, i);
        }
        return RetainerCaptureRules.ForSaleList(snapshot, order);
    }

    public void Reset() => loadState.Reset();
    public void Dispose()
    {
        foreach (var type in MenuEvents) Plugin.Addons.UnregisterListener(type, "SelectString", MenuChanged);
        inventoryHook?.Disable();
        marketHook?.Disable();
        inventoryHook?.Dispose();
        marketHook?.Dispose();
    }
}
