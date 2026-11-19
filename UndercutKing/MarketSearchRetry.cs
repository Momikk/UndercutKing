namespace UndercutKing;

// Bounded retries for an explicit server rejection; an empty successful market is not a failure.
public sealed class MarketSearchRetry
{
    public const uint RateLimited = 0x70000003;
    private readonly object gate = new();
    private (ulong Character, uint World, uint Item, nint Window) context;
    private int attempts;
    private DateTimeOffset? due;
    private DateTimeOffset? bindingDeadline;
    private DateTimeOffset? responseDeadline;
    private bool exhausted;

    public void Reset() { lock (gate) Clear(); }
    private void Clear() { context = default; attempts = 0; due = null; bindingDeadline = null; responseDeadline = null; exhausted = false; }

    public void Observe(ulong character, uint world, uint item, nint window, uint status, uint count, DateTimeOffset now)
    {
        lock (gate)
        {
            var next = (character, world, item, window);
            if (character == 0 || world == 0 || item == 0) { Clear(); return; }
            if (context != next) { Clear(); context = next; }
            if (status != RateLimited || count != 0) { Clear(); return; }
            if (exhausted || responseDeadline is { } response && now >= response)
            {
                exhausted = true;
                return;
            }
            responseDeadline = null;
            if (window == 0) bindingDeadline ??= now.AddSeconds(2);
            if (attempts >= 3) exhausted = true;
            // Repeated rejection packets cannot postpone a retry or reset its budget.
            if (due == null && attempts < 3) due = now.AddSeconds(1 << attempts);
        }
    }

    public bool Take(ulong character, uint world, uint item, nint window, bool eligible, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!eligible || context.Character != character || context.World != world || context.Item != item)
            { Clear(); return false; }
            if (context.Window == 0)
            {
                if (bindingDeadline is not { } deadline || now > deadline) { Clear(); return false; }
                if (window == 0) return false;
                context.Window = window;
                bindingDeadline = null;
            }
            else if (context.Window != window) { Clear(); return false; }
            if (exhausted) return false;
            if (responseDeadline is { } response && now >= response)
            {
                exhausted = true;
                return false;
            }
            if (due == null || now < due.Value) return false;
            due = null;
            attempts++;
            responseDeadline = now.AddSeconds(10);
            return true;
        }
    }

    public bool IsRecovering(ulong character, uint world, uint item, nint window, DateTimeOffset? now = null)
    {
        var time = now ?? DateTimeOffset.UtcNow;
        lock (gate)
            return character != 0 && context.Character == character && context.World == world && context.Item == item &&
                (context.Window == 0 || context.Window == window) && !exhausted &&
                (bindingDeadline == null || time <= bindingDeadline) &&
                (responseDeadline == null || time < responseDeadline) && (due != null || attempts != 0);
    }
}
