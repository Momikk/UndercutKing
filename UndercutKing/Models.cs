namespace UndercutKing;

public enum HighlightStatus { Normal, Undercut, Unknown }

public sealed record Listing(int Slot, int MenuIndex, uint ItemId, bool Hq, uint Quantity, uint Price);
public sealed class RetainerSnapshot
{
    public ulong CharacterId { get; set; }
    public ulong RetainerId { get; set; }
    public uint WorldId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public List<Listing> Listings { get; set; } = [];
}

public sealed record MarketQuote(uint? Nq, uint? Hq, DateTimeOffset ObservedAt, string Source, bool Complete = true);

public static class PriceRules
{
    // Only compare the same quality. Equal prices are competitive; never invent a price for missing data.
    public static HighlightStatus Evaluate(Listing listing, MarketQuote? quote, DateTimeOffset now, int minutes)
    {
        if (listing.Price == 0 || quote == null || now - quote.ObservedAt > TimeSpan.FromMinutes(minutes))
            return HighlightStatus.Unknown;
        var lowest = listing.Hq ? quote.Hq : quote.Nq;
        if (lowest == null) return quote.Complete ? HighlightStatus.Normal : HighlightStatus.Unknown;
        return listing.Price > lowest ? HighlightStatus.Undercut : HighlightStatus.Normal;
    }
}

public sealed record UndercutSummary(int Undercut, int Unknown, int ScannedRetainers)
{
    public string BarText => Unknown > 0 || ScannedRetainers == 0
        ? Undercut > 0 ? $"压价 {Undercut}+" : "压价 ?"
        : $"压价 {Undercut}";

    public static UndercutSummary Count(IEnumerable<RetainerSnapshot> snapshots, ulong character,
        Func<RetainerSnapshot, Listing, HighlightStatus> status)
    {
        if (character == 0) return new(0, 0, 0);
        var own = snapshots.Where(x => x.CharacterId == character)
            .GroupBy(x => x.RetainerId).Select(x => x.MaxBy(y => y.CapturedAt)!).ToArray();
        var undercut = 0;
        var unknown = 0;
        foreach (var snapshot in own)
            foreach (var listing in snapshot.Listings)
            {
                var value = status(snapshot, listing);
                if (value == HighlightStatus.Undercut) undercut++;
                else if (value == HighlightStatus.Unknown) unknown++;
            }
        return new(undercut, unknown, own.Length);
    }
}
