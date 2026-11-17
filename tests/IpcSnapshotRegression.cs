using System.Text.Json;
using UndercutKing;

internal static class IpcSnapshotRegression
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new Listing(4, -1, 100, false, 1, 1877);
        RetainerSnapshot[] saved =
        [
            new() { CharacterId = 1, RetainerId = 10, CapturedAt = now.AddDays(-1), Listings = [first with { Slot = 3 }] },
            new() { CharacterId = 1, RetainerId = 10, CapturedAt = now, Listings =
                [first, first with { Slot = 6, Price = 1878, Hq = true }, first with { Slot = 7, ForSale = false }] },
            new() { CharacterId = 2, RetainerId = 11, CapturedAt = now, Listings = [first] },
        ];
        var quote = new MarketQuote(1878, null, now, "test");
        var snapshot = IpcSnapshot.Create(1, [(10, "一号"), (11, "二号")], saved,
            (_, listing) => PriceRules.Evaluate(listing, quote, now, 15), new HashSet<uint> { 100 }, true, true);
        saved[1].Listings.Clear();
        using var document = JsonDocument.Parse(snapshot.ToJson());
        var root = document.RootElement;
        var retainers = root.GetProperty("retainers");
        var listings = retainers[0].GetProperty("listings");
        if (root.GetProperty("apiVersion").GetInt32() != 1 || root.GetProperty("characterId").GetString() != "1" ||
            retainers[0].GetProperty("retainerId").GetString() != "10" ||
            retainers[0].GetProperty("undercutCount").GetInt32() != 1 || listings.GetArrayLength() != 2 ||
            listings[0].GetProperty("slot").GetInt32() != 4 || listings[1].GetProperty("slot").GetInt32() != 6 ||
            listings[0].GetProperty("priceStatus").GetString() != "normal" ||
            listings[1].GetProperty("priceStatus").GetString() != "undercut" ||
            listings[1].GetProperty("highlightStatus").GetString() != "bagMatch" ||
            retainers[1].GetProperty("undercutCount").ValueKind != JsonValueKind.Null ||
            retainers[1].GetProperty("listings").GetArrayLength() != 0 || !snapshot.HasRefreshableListings ||
            IpcSnapshot.Empty.HasRefreshableListings)
            throw new Exception("IPC snapshot contract: ownership, distinct slots, color priority, and refresh eligibility");
        Console.WriteLine("PASS: IPC JSON contract, snapshot isolation and per-slot highlight data");
    }
}
