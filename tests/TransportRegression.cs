using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using UndercutKing;

internal static class TransportRegression
{
    public static async Task ProbeAsync()
    {
        using var socket = new System.Net.WebSockets.ClientWebSocket();
        socket.Options.SetRequestHeader("User-Agent", "UndercutKing/0.1.0.7-validation");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await socket.ConnectAsync(new Uri("wss://universalis.app/api/ws"), timeout.Token);
        foreach (var change in new[] { "add", "remove" })
            await socket.SendAsync(new ArraySegment<byte>(UniversalisProtocol.Subscribe(1167, change)), System.Net.WebSockets.WebSocketMessageType.Binary, true, timeout.Token);
        Console.WriteLine("LIVE: WebSocket handshake and BSON subscriptions sent for world 1167");
        var buffer = new byte[8192];
        try
        {
            while (!timeout.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                System.Net.WebSockets.WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) throw new IOException("Live feed closed the connection");
                    if (message.Length > 4 * 1024 * 1024) throw new IOException("Live feed message too large");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                var change = UniversalisProtocol.ReadChange(message.ToArray());
                if (change is { } value) { Console.WriteLine($"LIVE: decoded listing change world={value.World}, item={value.Item}"); return; }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Console.WriteLine("LIVE: handshake succeeded; no matching listing event arrived within the bounded observation window");
        }
    }

    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(string name, bool ok) { checks++; if (!ok) throw new Exception(name); }
        var now = DateTimeOffset.UtcNow;
        var q = new MarketRequestQueue();
        for (uint item = 1; item <= 100; item++) { q.Enqueue((1, item), now); q.Enqueue((1, item), now, pushed: true); }
        q.Enqueue((2, 1), now);
        Check("duplicate events merge", q.Count == 101);
        Check("five-second coalescing", q.Take(now.AddSeconds(4)) == null);
        var first = q.Take(now.AddSeconds(5))!.Value;
        Check("world isolation and fifty-item cap", first.World == 1 && first.Items.Length == 50 && first.Items.Distinct().Count() == 50);
        Check("global batch spacing", q.Take(now.AddSeconds(5.5)) == null);
        var second = q.Take(now.AddSeconds(6))!.Value;
        Check("remaining items not lost", second.Items.Length == 50 && !first.Items.Intersect(second.Items).Any());
        Check("other world gets its own batch", q.Take(now.AddSeconds(7))!.Value.World == 2);
        q.Enqueue((1, 1), now.AddSeconds(8), pushed: true);
        Check("push storm cooldown", q.Take(now.AddSeconds(34)) == null);
        Check("push follow-up after cooldown", q.Take(now.AddSeconds(35)) != null);
        q.Failed(now.AddSeconds(36), true, TimeSpan.FromMinutes(3));
        q.Enqueue((1, 2), now.AddSeconds(40)); // manual refresh uses the same queue
        Check("Retry-After enforced for all requests", q.Take(now.AddMinutes(2)) == null && q.PauseUntil == now.AddSeconds(216));
        Check("retry resumes after global pause", q.Take(now.AddSeconds(216)) != null);
        var q2 = new MarketRequestQueue();
        q2.Failed(now, true, null); Check("429 minimum minute", q2.PauseUntil == now.AddMinutes(1));
        q2.Failed(now.AddMinutes(1), true, null); Check("successive 429 backs off", q2.PauseUntil == now.AddMinutes(3));
        q2.Succeeded(); q2.Failed(now.AddMinutes(3), true, null); Check("success resets backoff", q2.PauseUntil == now.AddMinutes(4));
        q2.Enqueue((1, 1), now); q2.Enqueue((2, 2), now); q2.Retain(new HashSet<(uint, uint)> { (1, 1) });
        Check("logout or removed watch cancels queued work", q2.Count == 1);
        q2.ClearPending(); Check("disabling automatic queries clears pending work", q2.Count == 0);
        var subscription = BsonSerializer.Deserialize<BsonDocument>(UniversalisProtocol.Subscribe(1167, "add"));
        Check("BSON subscription channel", subscription["event"] == "subscribe" && subscription["channel"] == "listings/add{world=1167}");
        foreach (var kind in new[] { "listings/add", "listings/remove" })
            Check("decode listing delta", UniversalisProtocol.ReadChange(new BsonDocument { { "event", kind }, { "world", 1167 }, { "item", 100 } }.ToBson()) == (1167u, 100u));
        Check("ignore unrelated events", UniversalisProtocol.ReadChange(new BsonDocument { { "event", "sales/add" } }.ToBson()) == null);
        Check("reject invalid IDs", UniversalisProtocol.ReadChange(new BsonDocument { { "event", "listings/add" }, { "world", -1 }, { "item", 100 } }.ToBson()) == null);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { itemID = 100, lastUploadTime = now.ToUnixTimeMilliseconds(), listings = new[]
        {
            new { retainerName = "Own", hq = false, pricePerUnit = 1 },
            new { retainerName = "Other", hq = false, pricePerUnit = 900 },
            new { retainerName = "Other", hq = true, pricePerUnit = 800 },
        } }));
        var quote = UniversalisProtocol.ParseQuote(document.RootElement, 100, new HashSet<string> { "Own" }, now);
        Check("owner exclusion and NQ/HQ minimum", quote.Lowest == 800 && quote.Nq == 900);
        Check("actual upload time retained", quote.ObservedAt.ToUnixTimeMilliseconds() == now.ToUnixTimeMilliseconds());
        var mismatch = false;
        try { UniversalisProtocol.ParseQuote(document.RootElement, 101, new HashSet<string>(), now); } catch (InvalidDataException) { mismatch = true; }
        Check("mismatched item rejected", mismatch);

        var results = new ConcurrentDictionary<(uint World, uint Item), MarketQuote>();
        var handler = new BatchHandler();
        using (var client = new UniversalisClient((w, i) => results.GetValueOrDefault((w, i)), (w, i, v) => results[(w, i)] = v,
                   new HttpClient(handler), startFeed: false))
        {
            var watched = Enumerable.Range(1, 100).Select(x => (1u, (uint)x)).Append((2u, 1u)).ToArray();
            client.Watch(watched, ["Own"], false);
            foreach (var (world, item) in watched) { client.Queue(world, item, true, 15); client.Queue(world, item, true, 15); }
            client.Queue(3, 1, true, 15);
            var stop = DateTimeOffset.UtcNow.AddSeconds(20);
            while (results.Count < 101 && DateTimeOffset.UtcNow < stop) await Task.Delay(100);
            Check("real worker batches and parses all watched items", results.Count == 101 && handler.Requests.Count == 3);
            Check("worker merges qualities and excludes own prices", results.Values.All(x => x.Lowest == 800));
            var times = handler.Requests.Select(x => x.At).ToArray();
            Check("actual worker batches paced", times.Zip(times.Skip(1)).All(x => x.Second - x.First >= TimeSpan.FromMilliseconds(950)));
            Check("worker batch size capped", handler.Requests.All(x => x.Count <= 50));
            client.Watch(watched, ["Own"], true);
            Check("ignore untracked item and other world pushes", !client.NotifyChange((1u, 999u)) && !client.NotifyChange((3u, 1u)));
            for (var n = 0; n < 100; n++) client.NotifyChange((1u, 1u));
            Check("real push path coalesces a message storm", client.Status.Contains("待查询 1"));
            client.Watch([], [], false);
            Check("logout ignores push notifications", !client.NotifyChange((1u, 1u)));
            client.Queue(1, 1, true, 15);
            await Task.Delay(1100);
            Check("logged-out client does not query", handler.Requests.Count == 3);
        }
        var limitedHandler = new LimitedHandler();
        using (var limited = new UniversalisClient((_, _) => null, (_, _, _) => throw new Exception("A 429 must not publish a quote"),
                   new HttpClient(limitedHandler), startFeed: false))
        {
            limited.Watch([(1u, 1u), (1u, 2u)], [], false);
            limited.Queue(1, 1, true, 15);
            var stop = DateTimeOffset.UtcNow.AddSeconds(10);
            while (limitedHandler.Count == 0 && DateTimeOffset.UtcNow < stop) await Task.Delay(100);
            await Task.Delay(300);
            Check("real worker recognizes 429", limited.LastError.Contains("限流") && limited.Status.Contains("暂停"));
            limited.Queue(1, 2, true, 15);
            await Task.Delay(5500);
            Check("manual query cannot bypass real worker rate-limit pause", limitedHandler.Count == 1);
            limited.Watch([(1u, 1u), (1u, 2u)], [], false, automatic: false);
            Check("automatic disable clears real pending requests", limited.Status.Contains("待查询 0"));
        }
        Console.WriteLine($"PASS: {checks} batch, push protocol, rate-limit and HTTP worker scenarios");
    }

    private sealed class BatchHandler : HttpMessageHandler
    {
        public ConcurrentQueue<(DateTimeOffset At, int Count)> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var parts = request.RequestUri!.AbsolutePath.Split('/');
            var ids = parts[^1].Split(',').Select(uint.Parse).ToArray();
            Requests.Enqueue((DateTimeOffset.UtcNow, ids.Length));
            object Item(uint id) => new { itemID = id, lastUploadTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), listings = new[]
            {
                new { retainerName = "Own", hq = false, pricePerUnit = 1 },
                new { retainerName = "Other", hq = true, pricePerUnit = 800 },
                new { retainerName = "Other", hq = false, pricePerUnit = 900 },
            } };
            var payload = ids.Length == 1 ? Item(ids[0]) : new { items = ids.ToDictionary(x => x.ToString(), Item) };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") });
        }
    }
    private sealed class LimitedHandler : HttpMessageHandler
    {
        private int count;
        public int Count => count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(response);
        }
    }
}
