namespace UndercutKing;

// Pace completed frames, including work done by the game; never add a full frame's
// delay on top of that work. Clock and wait are injectable for deterministic tests.
internal sealed class FramePacer(Func<long> clock, Action<int> wait, long frequency)
{
    private long? previousFrame;
    internal static int ClampFps(int fps) => Math.Clamp(fps, 5, 240);

    internal void Update(bool enabled, int fps, Func<bool> isForeground)
    {
        if (!enabled || isForeground()) { previousFrame = null; return; }
        var now = clock();
        if (previousFrame is { } previous)
        {
            var interval = (double)frequency / ClampFps(fps);
            var remaining = interval - (now - previous);
            while (remaining > 0)
            {
                // Recheck focus at most 5 ms later so returning to the game does
                // not have to wait for a full background frame interval.
                wait((int)Math.Clamp(Math.Ceiling(remaining * 1000 / frequency), 1, 5));
                if (isForeground()) { previousFrame = null; return; }
                now = clock();
                remaining = interval - (now - previous);
            }
        }
        previousFrame = now;
    }
}
