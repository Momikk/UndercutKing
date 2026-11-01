namespace UndercutKing;

// Bounded retries for an explicit server rejection; an empty successful market is not a failure.
public sealed class MarketSearchRetry
{
    public const uint RateLimited = 0x70000003;
    private readonly object gate = new();
    private (ulong Character, uint World, uint Item, nint Window) context;
    private int attempts;
    private DateTimeOffset? due;

    public void Reset() { lock (gate) Clear(); }
    private void Clear() { context = default; attempts = 0; due = null; }

    public void Observe(ulong character, uint world, uint item, nint window, uint status, uint count, DateTimeOffset now)
    {
        lock (gate)
        {
            var next = (character, world, item, window);
            if (character == 0 || world == 0 || item == 0 || window == 0) { Clear(); return; }
            if (context != next) { Clear(); context = next; }
            if (status != RateLimited || count != 0) { Clear(); return; }
            // Repeated rejection packets cannot postpone a retry or reset its budget.
            if (due == null && attempts < 3) due = now.AddSeconds(1 << attempts);
        }
    }

    public bool Take(ulong character, uint world, uint item, nint window, bool eligible, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!eligible || context != (character, world, item, window)) { Clear(); return false; }
            if (due == null || now < due.Value) return false;
            due = null;
            attempts++;
            return true;
        }
    }
}
