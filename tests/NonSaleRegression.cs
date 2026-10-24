using System.Text.Json;
using UndercutKing;

internal static class NonSaleRegression
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
        var now = DateTimeOffset.UtcNow;
        var display = new Listing(0, 0, 100, false, 1, 1) { ForSale = false };
        var ordinary = new Listing(1, 1, 100, false, 1, 1000) { ForSale = true };
        var cheap = ordinary with { Price = 1 };
        var quote = new MarketQuote(900, null, now, "test");
        Check(PriceRules.Evaluate(display, null, now, 1) == HighlightStatus.Normal, "Display-only item must not become stale yellow");
        Check(PriceRules.Evaluate(display with { Price = 9999 }, quote, now, 1) == HighlightStatus.Normal, "Explicit non-sale state must suppress undercut red regardless of stored price");
        Check(PriceRules.Evaluate(cheap, null, now, 1) == HighlightStatus.Unknown && cheap.IsForSale, "Genuine one-gil listings must not be excluded by a price heuristic");
        var bags = new HashSet<uint> { 100 };
        Check(BagMatchRules.Display(display, HighlightStatus.Unknown, bags, true, true) == HighlightStatus.Normal, "Display-only items must not turn blue either");
        var snapshot = new RetainerSnapshot { CharacterId = 1, RetainerId = 10, Listings = [display, ordinary] };
        HighlightStatus Status(RetainerSnapshot _, Listing x) => PriceRules.Evaluate(x, quote, now, 2880);
        Check(UndercutSummary.Count([snapshot], 1, Status).Undercut == 1, "A real listing of the same item must still count and compare");
        snapshot.Listings.Remove(ordinary);
        Check(UndercutSummary.Count([snapshot], 1, (_, x) => PriceRules.Evaluate(x, null, now, 1)) is { Undercut: 0, Unknown: 0 }, "Only display items must not leave unknown counts");
        Check(!BagMatchRules.MatchesRetainer([snapshot], 1, 10, bags), "Display-only items must not mark the retainer blue");
        Check(BagOverlayRules.Targets(snapshot, 1, 10, true, true, true, bags).Count == 0, "Display-only items must not trigger mirrored bag highlights");
        Check(!ListingEligibility.Preserve(display with { MenuIndex = -1, ForSale = null }, display).IsForSale, "Remember confirmed display state after closing the sell UI");
        Check(ListingEligibility.Preserve(display with { ForSale = true }, display).IsForSale, "A newly confirmed sale must override previous display state");
        foreach (var changed in new[] { display with { ItemId = 200 }, display with { Slot = 1 }, display with { Hq = true }, display with { Quantity = 2 }, display with { Price = 10 } })
            Check(ListingEligibility.Preserve(changed with { ForSale = null }, display).ForSale == null, "Do not inherit old display state across slot contents or price changes");
        var saved = JsonSerializer.Deserialize<Listing>(JsonSerializer.Serialize(display));
        Check(saved?.ForSale == false, "Confirmed display flag must survive restart serialization");
        Check(JsonSerializer.Deserialize<Listing>("{\"Slot\":0,\"MenuIndex\":0,\"ItemId\":100,\"Hq\":false,\"Quantity\":1,\"Price\":1}")?.IsForSale == true,
            "Legacy records must remain eligible until explicitly identified, including one-gil sales");
        Console.WriteLine($"PASS: {checks} mannequin non-sale filtering and persistence scenarios");
    }
}
