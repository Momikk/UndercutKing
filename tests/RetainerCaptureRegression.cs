using UndercutKing;

internal static class RetainerCaptureRegression
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var loading = new RetainerLoadState();
        Check(!loading.Ready(10), "No packet must mean no fresh snapshot");
        loading.Received(10);
        Check(loading.Ready(10), "Current retainer packet should allow capture");
        Check(!loading.Ready(20), "Switching retainers must invalidate the old container");
        Check(!loading.Ready(10), "Switching back must require another packet");
        loading.Received(10); loading.Observe(0);
        Check(!loading.Ready(10), "Dismiss/re-summon must require fresh data");
        loading.Received(10); loading.Reset();
        Check(!loading.Ready(10), "Character change must clear load ownership");
        var snapshot = new RetainerSnapshot
        {
            CharacterId = 1, RetainerId = 10, WorldId = 1167, Name = "test",
            Listings = [new(2, -1, 100, false, 99, 1000), new(7, -1, 200, true, 1, 1000)],
        };
        HighlightStatus Status(RetainerSnapshot _, Listing listing) => PriceRules.Evaluate(listing,
            new(900, null, DateTimeOffset.UtcNow, "test"), DateTimeOffset.UtcNow, 2880);
        Check(UndercutSummary.Count([snapshot], 1, Status).Undercut == 2, "Summon snapshot must count without UI order");
        snapshot.Listings.RemoveAt(0);
        Check(UndercutSummary.Count([snapshot], 1, Status).Undercut == 1, "Sold slot must disappear from count before sell UI");
        var mapped = RetainerCaptureRules.ForSaleList(snapshot, new Dictionary<int, int> { [7] = 0 });
        Check(mapped?.Listings.Single().MenuIndex == 0 && mapped.RetainerId == 10 && snapshot.Listings.Single().MenuIndex == -1,
            "UI order must map physical slots without changing saved snapshot");
        Check(RetainerCaptureRules.ForSaleList(snapshot, new Dictionary<int, int>()) == null, "Incomplete order must not highlight a guessed row");
        Check(RetainerCaptureRules.ForSaleList(snapshot, new Dictionary<int, int> { [7] = 20 }) == null, "Out-of-bounds row must be rejected");
        Check(RetainerCaptureRules.ForSaleList(snapshot, new Dictionary<int, int> { [7] = 0, [2] = 0 }) == null, "Duplicate visible rows must be rejected");
        snapshot.Listings.Clear();
        Check(UndercutSummary.Count([snapshot], 1, Status).Undercut == 0 &&
            RetainerCaptureRules.ForSaleList(snapshot, new Dictionary<int, int>()) != null, "Confirmed empty retainer must clear counts");
        Console.WriteLine($"PASS: {checks} summon capture ownership, sold-slot counts and live UI mapping scenarios");
    }
}
