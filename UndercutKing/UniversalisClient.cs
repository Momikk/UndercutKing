using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;

namespace UndercutKing;

internal sealed class UniversalisClient : IDisposable
{
    private readonly object gate = new();
    private readonly MarketRequestQueue queue = new();
    private readonly HttpClient http;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Func<uint, uint, MarketQuote?> getQuote;
    private readonly Action<uint, uint, MarketQuote> putQuote;
    private readonly Task worker;
    private readonly Task feed;
    private HashSet<(uint World, uint Item)> watched = [];
    private IReadOnlySet<string> owners = new HashSet<string>();
    private bool pushEnabled;
    private bool automaticEnabled = true;
    private ClientWebSocket? socket;
    private volatile bool connected;
    public string LastError { get; private set; } = "";
    public string PushError { get; private set; } = "";
    public bool Connected => connected;
    public string Status
    {
        get
        {
            lock (gate)
                return DateTimeOffset.UtcNow < queue.PauseUntil ? $"查询暂停至 {queue.PauseUntil.ToLocalTime():HH:mm:ss}，待查询 {queue.Count} 种物品"
                    : $"消息推送：{(pushEnabled ? connected ? "已连接" : "连接中／重连中" : "已关闭")}；待查询 {queue.Count} 种物品";
        }
    }

    public UniversalisClient(Func<uint, uint, MarketQuote?> getQuote, Action<uint, uint, MarketQuote> putQuote, HttpClient? http = null, bool startFeed = true)
    {
        this.getQuote = getQuote; this.putQuote = putQuote;
        this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        this.http.DefaultRequestHeaders.UserAgent.ParseAdd("UndercutKing/0.1.0.7 (+https://github.com/Momikk/UndercutKing)");
        worker = Task.Run(RunRequests);
        feed = startFeed ? Task.Run(RunFeed) : Task.CompletedTask;
    }

    public void Watch(IEnumerable<(uint World, uint Item)> items, IEnumerable<string> ownNames, bool enablePush, bool automatic = true)
    {
        var next = items.Where(x => x.World != 0 && x.Item != 0).ToHashSet();
        lock (gate)
        {
            var worldsChanged = !watched.Select(x => x.World).ToHashSet().SetEquals(next.Select(x => x.World));
            if (worldsChanged || pushEnabled != enablePush) socket?.Abort();
            if (!automatic && automaticEnabled) queue.ClearPending();
            automaticEnabled = automatic;
            watched = next; owners = ownNames.ToHashSet(StringComparer.Ordinal); pushEnabled = enablePush && next.Count > 0;
            queue.Retain(watched);
        }
    }

    public void Queue(uint world, uint item, bool force, int minutes)
    {
        lock (gate)
        {
            var key = (world, item);
            if (!watched.Contains(key) || lifetime.IsCancellationRequested || !force && !automaticEnabled) return;
            var now = DateTimeOffset.UtcNow;
            if (MarketRefreshRules.ShouldRequest(now, queue.LastAttempt(key), getQuote(world, item), force, minutes)) queue.Enqueue(key, now);
        }
    }

    internal bool NotifyChange((uint World, uint Item)? key)
    {
        lock (gate)
        {
            if (!pushEnabled || !automaticEnabled || key is not { } value || !watched.Contains(value)) return false;
            queue.Enqueue(value, DateTimeOffset.UtcNow, pushed: true);
            return true;
        }
    }

    private async Task RunRequests()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                (uint World, uint[] Items, int Attempts)? batch;
                IReadOnlySet<string> ownNames;
                lock (gate) { batch = queue.Take(DateTimeOffset.UtcNow); ownNames = owners; }
                if (batch == null) { await Task.Delay(250, lifetime.Token); continue; }
                var (world, items, attempt) = batch.Value;
                try
                {
                    using var response = await http.GetAsync($"https://universalis.app/api/v2/{world}/{string.Join(',', items)}?listings=100&entries=0", lifetime.Token);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retry = response.Headers.RetryAfter;
                        var delay = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null);
                        Fail(world, items, attempt, true, delay, "Universalis 限流，已暂停全部查询（包括手动刷新）。");
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));
                    var root = doc.RootElement;
                    var received = new List<(uint Item, MarketQuote Quote)>();
                    foreach (var item in items)
                    {
                        var value = root;
                        if (items.Length > 1 && (!root.TryGetProperty("items", out var values) || !values.TryGetProperty(item.ToString(), out value)))
                            throw new InvalidDataException("批量响应缺少物品，保留已有报价");
                        received.Add((item, UniversalisProtocol.ParseQuote(value, item, ownNames, DateTimeOffset.UtcNow)));
                    }
                    foreach (var result in received) putQuote(world, result.Item, result.Quote);
                    lock (gate) queue.Succeeded();
                    LastError = "";
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception ex) { Fail(world, items, attempt, false, null, $"市场查询失败，已延后重试：{ex.Message}"); }
                // At least one second between completed batches, including manual queries.
                await Task.Delay(1000, lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private void Fail(uint world, uint[] items, int attempt, bool rateLimited, TimeSpan? retry, string error)
    {
        LastError = error;
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            queue.Failed(now, rateLimited, retry);
            if (attempt < 2)
                foreach (var item in items) if (watched.Contains((world, item))) queue.Enqueue((world, item), now, retry: attempt + 1);
        }
    }

    private async Task RunFeed()
    {
        var failures = 0;
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                uint[] worlds;
                lock (gate) worlds = pushEnabled ? watched.Select(x => x.World).Distinct().Order().ToArray() : [];
                if (worlds.Length == 0) { await Task.Delay(500, lifetime.Token); continue; }
                using var client = new ClientWebSocket();
                client.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                client.Options.KeepAliveTimeout = TimeSpan.FromSeconds(30);
                client.Options.SetRequestHeader("User-Agent", "UndercutKing/0.1.0.7");
                lock (gate) socket = client;
                try
                {
                    using var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    handshake.CancelAfter(TimeSpan.FromSeconds(20));
                    await client.ConnectAsync(new Uri("wss://universalis.app/api/ws"), handshake.Token);
                    foreach (var world in worlds)
                        foreach (var change in new[] { "add", "remove" })
                            await client.SendAsync(new ArraySegment<byte>(UniversalisProtocol.Subscribe(world, change)), WebSocketMessageType.Binary, true, lifetime.Token);
                    connected = true; PushError = ""; failures = 0;
                    // Initial subscription and reconnection both need a full baseline, including missed events.
                    lock (gate) foreach (var key in watched) queue.Enqueue(key, DateTimeOffset.UtcNow, pushed: true);
                    var buffer = new byte[8192];
                    while (client.State == WebSocketState.Open)
                    {
                        using var message = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token);
                            if (result.MessageType == WebSocketMessageType.Close) throw new IOException("推送连接已关闭");
                            if (message.Length + result.Count > 4 * 1024 * 1024) throw new InvalidDataException("推送消息过大");
                            message.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);
                        if (result.MessageType != WebSocketMessageType.Binary) continue;
                        var key = UniversalisProtocol.ReadChange(message.ToArray());
                        // Events are deltas, not a full market snapshot. Coalesce a bounded full-price query.
                        NotifyChange(key);
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception ex) { PushError = $"推送暂不可用，继续定时查询：{ex.Message}"; failures = Math.Min(failures + 1, 6); }
                finally { connected = false; lock (gate) if (ReferenceEquals(socket, client)) socket = null; }
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, failures)) + Random.Shared.Next(0, 4)), lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        lifetime.Cancel(); lock (gate) socket?.Abort();
        // Do not block the game thread; finish cancellation before disposing transport resources.
        _ = Task.WhenAll(worker, feed).ContinueWith(_ => { http.Dispose(); lifetime.Dispose(); }, TaskScheduler.Default);
    }
}
