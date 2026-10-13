using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Dalamud.Game.Network.Structures;

namespace UndercutKing;

internal sealed class MarketPrices : IDisposable
{
    private readonly ConcurrentDictionary<(uint World, uint Item), MarketQuote> quotes = new();
    private readonly ConcurrentDictionary<(uint World, uint Item), DateTimeOffset> lastAttempts = new();
    private readonly ConcurrentDictionary<(uint World, uint Item), byte> pending = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim requests = new(2);
    private readonly List<IMarketBoardItemListing> offerings = [];
    private readonly object gameGate = new();
    private int? requestId;
    private uint? expected;
    private uint requestWorld;
    private uint requestItem;
    private DateTimeOffset requestStarted;
    private HashSet<ulong> ownedIds = [];
    private HashSet<string> ownedNames = [];
    public string LastError { get; private set; } = "";

    public MarketPrices() => http.DefaultRequestHeaders.UserAgent.ParseAdd("UndercutKing/0.1.0.0 (+https://github.com/Momikk/UndercutKing)");
    public MarketQuote? Get(uint world, uint item) => quotes.GetValueOrDefault((world, item));

    public void SetOwners(IEnumerable<ulong> ids, IEnumerable<string> names)
    {
        ownedIds = ids.ToHashSet();
        ownedNames = names.ToHashSet(StringComparer.Ordinal);
    }

    public void Queue(uint world, uint item, bool force, int freshMinutes)
    {
        if (world == 0 || item == 0 || lifetime.IsCancellationRequested) return;
        var key = (world, item);
        var now = DateTimeOffset.UtcNow;
        if (!force && Get(world, item) is { } existing && now - existing.ObservedAt < TimeSpan.FromMinutes(freshMinutes)) return;
        // Protect the public API even when a user repeatedly presses refresh or upstream data is old.
        if (lastAttempts.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(force ? 5 : 60)) return;
        if (!pending.TryAdd(key, 0)) return;
        lastAttempts[key] = now;
        var owners = ownedNames.ToHashSet(StringComparer.Ordinal);
        _ = Fetch(world, item, owners);
    }

    private async Task Fetch(uint world, uint item, HashSet<string> owners)
    {
        var entered = false;
        try
        {
            await requests.WaitAsync(lifetime.Token).ConfigureAwait(false);
            entered = true;
            // Request all listings so both NQ and HQ minima can be determined independently.
            using var response = await http.GetAsync($"https://universalis.app/api/v2/{world}/{item}?listings=100&entries=0", lifetime.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(lifetime.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: lifetime.Token).ConfigureAwait(false);
            var root = doc.RootElement;
            if (!root.TryGetProperty("itemID", out var id) || id.GetUInt32() != item || !root.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("市场响应缺少完整物品数据");
            uint? nq = null, hq = null;
            foreach (var listing in listings.EnumerateArray())
            {
                if (listing.TryGetProperty("retainerName", out var name) && owners.Contains(name.GetString() ?? "")) continue;
                var price = listing.GetProperty("pricePerUnit").GetUInt32();
                if (price == 0) continue;
                if (listing.GetProperty("hq").GetBoolean()) hq = hq == null ? price : Math.Min(hq.Value, price);
                else nq = nq == null ? price : Math.Min(nq.Value, price);
            }
            var uploaded = root.TryGetProperty("lastUploadTime", out var time) ? DateTimeOffset.FromUnixTimeMilliseconds(time.GetInt64()) : DateTimeOffset.MinValue;
            if (uploaded > DateTimeOffset.UtcNow.AddMinutes(5)) throw new InvalidDataException("市场数据时间异常");
            // A missing quality in a truncated page is unknown, not evidence of no competition.
            Put(world, item, new(nq, hq, uploaded, "Universalis", listings.GetArrayLength() < 100));
            LastError = "";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { LastError = $"市场查询失败：{ex.Message}"; }
        finally
        {
            if (entered) requests.Release();
            pending.TryRemove((world, item), out _);
        }
    }

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

    public void Dispose()
    {
        lifetime.Cancel();
        http.Dispose();
        // Outstanding asynchronous requests release the semaphore themselves after cancellation.
    }
}
