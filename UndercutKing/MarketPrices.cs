using System.Collections.Concurrent;
using Dalamud.Game.Network.Structures;

namespace UndercutKing;

internal sealed class MarketPrices : IDisposable
{
    private readonly ConcurrentDictionary<(uint World, uint Item), MarketQuote> quotes = new();
    private readonly UniversalisClient client;
    private readonly List<IMarketBoardItemListing> offerings = [];
    private readonly object gameGate = new();
    private int? requestId;
    private uint? expected;
    private uint requestWorld;
    private uint requestItem;
    private DateTimeOffset requestStarted;
    private HashSet<ulong> ownedIds = [];
    private HashSet<string> ownedNames = [];
    public string LastError => client.LastError;
    public string PushError => client.PushError;
    public string NetworkStatus => client.Status;
    public bool PushConnected => client.Connected;

    public MarketPrices() => client = new(Get, Put);
    public MarketQuote? Get(uint world, uint item) => quotes.GetValueOrDefault((world, item));

    public void SetOwners(IEnumerable<ulong> ids, IEnumerable<string> names)
    {
        ownedIds = ids.ToHashSet();
        ownedNames = names.ToHashSet(StringComparer.Ordinal);
    }

    public void Watch(IEnumerable<(uint World, uint Item)> items, bool push, bool automatic = true) => client.Watch(items, ownedNames, push, automatic);
    public void Queue(uint world, uint item, bool force, int refreshMinutes) => client.Queue(world, item, force, refreshMinutes);

    private void Put(uint world, uint item, MarketQuote quote) => quotes.AddOrUpdate((world, item), quote,
        (_, old) => quote.ObservedAt >= old.ObservedAt ? quote : old);

    // Adapted from Allagan Market's complete-response accumulation; never use a partial page as the market minimum.
    public void Begin(uint status, uint count, uint world, uint item)
    {
        lock (gameGate) BeginCore(status, count, world, item);
    }

    private void BeginCore(uint status, uint count, uint world, uint item)
    {
        offerings.Clear();
        requestId = null;
        expected = null;
        if (status != 0 || count > 100 || world == 0) return;
        requestWorld = world;
        requestItem = item;
        requestStarted = DateTimeOffset.UtcNow;
        if (count == 0)
        {
            if (item != 0) Put(world, item, new(null, null, requestStarted, "游戏市场"));
            return;
        }
        expected = count;
    }

    public void Receive(IMarketBoardCurrentOfferings page)
    {
        lock (gameGate) ReceiveCore(page);
    }

    private void ReceiveCore(IMarketBoardCurrentOfferings page)
    {
        if (expected == null || DateTimeOffset.UtcNow - requestStarted > TimeSpan.FromSeconds(20)) return;
        if (requestId != null && requestId != page.RequestId) { expected = null; offerings.Clear(); return; }
        requestId = page.RequestId;
        offerings.AddRange(page.ItemListings);
        if (offerings.Count < expected) return;
        expected = null;
        var items = offerings.Select(x => x.ItemId).Distinct().ToArray();
        if (items.Length != 1 || (requestItem != 0 && requestItem != items[0])) { offerings.Clear(); return; }
        var others = offerings.Where(x => !ownedIds.Contains(x.RetainerId)).ToArray();
        var nq = others.Where(x => !x.IsHq).Select(x => (uint?)x.PricePerUnit).Min();
        var hq = others.Where(x => x.IsHq).Select(x => (uint?)x.PricePerUnit).Min();
        Put(requestWorld, items[0], new(nq, hq, requestStarted, "游戏市场"));
        offerings.Clear();
    }

    public void Dispose() => client.Dispose();
}
