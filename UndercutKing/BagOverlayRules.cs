namespace UndercutKing;

public static class BagOverlayRules
{
    public static HashSet<uint> Targets(RetainerSnapshot? snapshot, ulong character, ulong activeRetainer,
        bool salesVisible, bool blueEnabled, bool mirrorEnabled, IReadOnlySet<uint> bagItems)
    {
        if (!salesVisible || !blueEnabled || !mirrorEnabled || character == 0 || activeRetainer == 0 ||
            snapshot == null || snapshot.CharacterId != character || snapshot.RetainerId != activeRetainer) return [];
        return snapshot.Listings.Where(x => bagItems.Contains(x.ItemId)).Select(x => x.ItemId).ToHashSet();
    }

    public static bool ValidSource(int page, int slot) => page is >= 0 and < 4 && slot is >= 0 and < 35;
}
