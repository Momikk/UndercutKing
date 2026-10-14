namespace UndercutKing;

public static class BagMatchRules
{
    // InventoryItem.ItemId is the base ID; HQ is a separate flag and is intentionally ignored.
    public static HashSet<uint> ItemIds(IEnumerable<(uint ItemId, uint Quantity)> slots) =>
        slots.Where(x => x.ItemId != 0 && x.Quantity > 0).Select(x => x.ItemId).ToHashSet();

    public static bool MatchesRetainer(IEnumerable<RetainerSnapshot> snapshots, ulong characterId, ulong retainerId, IReadOnlySet<uint> bagItems)
    {
        if (characterId == 0 || retainerId == 0 || bagItems.Count == 0) return false;
        var snapshot = snapshots.Where(x => x.CharacterId == characterId && x.RetainerId == retainerId)
            .OrderByDescending(x => x.CapturedAt).FirstOrDefault();
        return snapshot?.Listings.Any(x => bagItems.Contains(x.ItemId)) == true;
    }

    public static HighlightStatus Display(Listing? listing, HighlightStatus priceStatus, IReadOnlySet<uint> bagItems,
        bool priceHighlight, bool bagHighlight)
    {
        if (listing == null) return HighlightStatus.Normal;
        if (bagHighlight && bagItems.Contains(listing.ItemId)) return HighlightStatus.BagMatch;
        return priceHighlight ? priceStatus : HighlightStatus.Normal;
    }
}
