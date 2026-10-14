namespace UndercutKing;

public static class BagMatchRules
{
    // InventoryItem.ItemId is the base ID; HQ is a separate flag and is intentionally ignored.
    public static HashSet<uint> ItemIds(IEnumerable<(uint ItemId, uint Quantity)> slots) =>
        slots.Where(x => x.ItemId != 0 && x.Quantity > 0).Select(x => x.ItemId).ToHashSet();

    public static HighlightStatus Display(Listing? listing, HighlightStatus priceStatus, IReadOnlySet<uint> bagItems,
        bool priceHighlight, bool bagHighlight)
    {
        if (listing == null) return HighlightStatus.Normal;
        if (bagHighlight && bagItems.Contains(listing.ItemId)) return HighlightStatus.BagMatch;
        return priceHighlight ? priceStatus : HighlightStatus.Normal;
    }
}
