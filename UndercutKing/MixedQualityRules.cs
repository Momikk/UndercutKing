namespace UndercutKing;

internal readonly record struct BagItem(uint ItemId, uint Quantity, bool Hq);

internal static class MixedQualityRules
{
    internal static HashSet<uint> Targets(IEnumerable<BagItem> items, bool enabled = true)
    {
        if (!enabled) return [];
        var qualities = new Dictionary<uint, int>();
        foreach (var item in items)
        {
            if (item.ItemId == 0 || item.Quantity == 0) continue;
            qualities.TryGetValue(item.ItemId, out var flags);
            qualities[item.ItemId] = flags | (item.Hq ? 2 : 1);
        }
        return qualities.Where(x => x.Value == 3).Select(x => x.Key).ToHashSet();
    }
}
