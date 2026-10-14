namespace UndercutKing;

public static class MarketRefreshRules
{
    // Poll cadence is independent of the validity window used for yellow highlights.
    // Throttle attempts as well as successful quotes so stale uploads or failures cannot cause a retry loop.
    public static bool ShouldRequest(DateTimeOffset now, DateTimeOffset? lastAttempt,
        MarketQuote? quote, bool force, int intervalMinutes)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, 1, 60));
        if (lastAttempt is { } last && now - last < (force ? TimeSpan.FromSeconds(5) : interval)) return false;
        return force || quote == null || now - quote.ObservedAt >= interval;
    }
}
