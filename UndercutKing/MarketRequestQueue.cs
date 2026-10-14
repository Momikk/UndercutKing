namespace UndercutKing;

// Accessed under UniversalisClient's gate; deterministic timestamps also allow regression tests.
public sealed class MarketRequestQueue
{
    private sealed record Request(DateTimeOffset Due, int Attempts);
    private readonly Dictionary<(uint World, uint Item), Request> pending = [];
    private readonly Dictionary<(uint World, uint Item), DateTimeOffset> attempts = [];
    public DateTimeOffset PauseUntil { get; private set; }
    private DateTimeOffset nextBatch;
    private int failures;
    public int Count => pending.Count;
    public void ClearPending() => pending.Clear();
    public DateTimeOffset? LastAttempt((uint World, uint Item) key) => attempts.TryGetValue(key, out var time) ? time : null;

    public void Enqueue((uint World, uint Item) key, DateTimeOffset now, bool pushed = false, int retry = 0)
    {
        if (key.World == 0 || key.Item == 0) return;
        var due = now.AddSeconds(5);
        if (pushed && LastAttempt(key) is { } last && last.AddSeconds(30) > due) due = last.AddSeconds(30);
        if (pending.TryGetValue(key, out var existing))
            pending[key] = existing with { Due = existing.Due < due ? existing.Due : due, Attempts = Math.Max(existing.Attempts, retry) };
        else pending[key] = new(due, retry);
    }

    public (uint World, uint[] Items, int Attempts)? Take(DateTimeOffset now)
    {
        if (now < PauseUntil || now < nextBatch) return null;
        var ready = pending.Where(x => x.Value.Due <= now).OrderBy(x => x.Value.Due).ToArray();
        if (ready.Length == 0) return null;
        var world = ready[0].Key.World;
        var batch = ready.Where(x => x.Key.World == world).Take(50).ToArray();
        foreach (var entry in batch) { pending.Remove(entry.Key); attempts[entry.Key] = now; }
        nextBatch = now.AddSeconds(1);
        return (world, batch.Select(x => x.Key.Item).ToArray(), batch.Max(x => x.Value.Attempts));
    }

    public void Retain(IReadOnlySet<(uint World, uint Item)> watched)
    {
        foreach (var key in pending.Keys.Where(x => !watched.Contains(x)).ToArray()) pending.Remove(key);
        foreach (var key in attempts.Keys.Where(x => !watched.Contains(x)).ToArray()) attempts.Remove(key);
    }

    public void Failed(DateTimeOffset now, bool rateLimited, TimeSpan? retryAfter)
    {
        failures = Math.Min(failures + 1, 5);
        var wait = TimeSpan.FromSeconds(Math.Min(900, (rateLimited ? 60 : 30) * Math.Pow(2, failures - 1)));
        if (retryAfter > wait) wait = retryAfter.Value;
        if (now + wait > PauseUntil) PauseUntil = now + wait;
    }
    public void Succeeded() => failures = 0;
}
