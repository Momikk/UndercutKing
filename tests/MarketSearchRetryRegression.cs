using UndercutKing;

internal static class MarketSearchRetryRegression
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var checks = 0;
        void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        var retry = new MarketSearchRetry();
        void Reject(DateTimeOffset at) => retry.Observe(1, 2, 100, 123, MarketSearchRetry.RateLimited, 0, at);
        bool Take(DateTimeOffset at) => retry.Take(1, 2, 100, 123, true, at);
        Check(!Take(now), "No retry without explicit rejection");
        Reject(now);
        Reject(now.AddMilliseconds(900));
        Check(!Take(now.AddMilliseconds(999)), "Wait before retry");
        Check(Take(now.AddSeconds(1)), "Duplicate rejection must not postpone retry");
        Check(!Take(now.AddSeconds(2)), "Only one retry per rejection");
        Reject(now.AddSeconds(2));
        Check(!Take(now.AddSeconds(3)), "Second retry backs off");
        Check(Take(now.AddSeconds(4)), "Second retry after two seconds");
        Reject(now.AddSeconds(5));
        Check(!Take(now.AddSeconds(8)), "Third retry backs off");
        Check(Take(now.AddSeconds(9)), "Third retry after four seconds");
        Reject(now.AddSeconds(10));
        Check(!Take(now.AddHours(1)), "Three retry limit");
        retry.Reset(); Reject(now);
        retry.Observe(1, 2, 100, 123, 0, 0, now);
        Check(!Take(now.AddSeconds(5)), "Successful empty market cancels retries");
        Reject(now); retry.Observe(1, 2, 100, 123, 42, 0, now);
        Check(!Take(now.AddSeconds(5)), "Other errors are not retried");
        foreach (var context in new[] { (2UL, 2U, 100U, (nint)123), (1UL, 3U, 100U, (nint)123), (1UL, 2U, 101U, (nint)123), (1UL, 2U, 100U, (nint)456) })
        {
            Reject(now);
            Check(!retry.Take(context.Item1, context.Item2, context.Item3, context.Item4, true, now.AddSeconds(5)), "Changed context must cancel");
            Check(!Take(now.AddSeconds(6)), "Cancelled context must not resume");
        }
        Reject(now);
        Check(!retry.Take(1, 2, 100, 123, false, now.AddSeconds(5)), "Disabled or hidden UI cancels");
        Check(!Take(now.AddSeconds(6)), "Re-enabling does not resume old retry");
        Reject(now); retry.Reset();
        Check(!Take(now.AddSeconds(6)), "Dispose/reset cancels");
        Console.WriteLine($"PASS: {checks} bounded market search retry scenarios");
    }
}
