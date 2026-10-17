namespace UndercutKing;

internal sealed class RetainerLoadState
{
    private ulong active;
    private ulong loaded;

    internal void Observe(ulong id)
    {
        if (active == id) return;
        active = id;
        loaded = 0;
    }

    internal void Received(ulong id) { Observe(id); loaded = id; }
    internal bool Ready(ulong id) { Observe(id); return id != 0 && loaded == id; }
    internal void Reset() { active = 0; loaded = 0; }
}

internal static class RetainerCaptureRules
{
    // The saved snapshot uses physical slots. Only a complete current UI mapping
    // may turn it into a snapshot for coloring visible rows.
    internal static RetainerSnapshot? ForSaleList(RetainerSnapshot snapshot, IReadOnlyDictionary<int, int> order)
    {
        if (order.Any(x => x.Key is < 0 or >= 20 || x.Value is < 0 or >= 20) ||
            order.Values.Distinct().Count() != order.Count ||
            snapshot.Listings.Any(x => !order.ContainsKey(x.Slot))) return null;
        return new RetainerSnapshot
        {
            CharacterId = snapshot.CharacterId, RetainerId = snapshot.RetainerId,
            WorldId = snapshot.WorldId, Name = snapshot.Name, CapturedAt = snapshot.CapturedAt,
            Listings = snapshot.Listings.Select(x => x with { MenuIndex = order[x.Slot] }).ToList(),
        };
    }
}
