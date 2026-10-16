using UndercutKing;

internal static class FramePacerRegression
{
    internal static void Run()
    {
        long now = 0;
        var foreground = false;
        var waits = 0;
        Action? afterWait = null;
        var pacer = new FramePacer(() => now, ms =>
        {
            if (ms is < 1 or > 5) throw new Exception("Uninterruptible frame wait");
            now += ms; waits++; afterWait?.Invoke();
        }, 1000);
        void Check(bool valid, string reason) { if (!valid) throw new Exception(reason); }
        void Tick(bool enabled = true, int fps = 20) => pacer.Update(enabled, fps, () => foreground);
        Tick();
        Check(now == 0 && waits == 0, "First background frame must not pause");
        now += 10; Tick();
        Check(now == 50, "20 fps must include game work in the 50 ms interval");
        now += 10; Tick();
        Check(now == 100, "Background frames must remain evenly paced");
        now += 80; Tick();
        Check(now == 180, "Already slow frames must not receive an extra delay");
        foreground = true; Tick();
        Check(now == 180, "Foreground must never wait");
        foreground = false; Tick();
        Check(now == 180, "Returning to background must start a new interval");
        afterWait = () => foreground = true;
        Tick( true, 5);
        Check(now == 185, "Returning to foreground must interrupt even a 5 fps wait within 5 ms");
        afterWait = null; foreground = false; Tick();
        now += 10; Tick(false);
        Check(now == 195, "Disabled limiter must never wait");
        Tick(); now += 10; Tick(true, 40);
        Check(now == 220, "New custom cap must take effect on the next frame");
        Check(FramePacer.ClampFps(0) == 5 && FramePacer.ClampFps(int.MaxValue) == 240, "Invalid saved/input caps must be bounded");
        Console.WriteLine("PASS: background frame pacing, focus transitions, disabled state, slow frames and custom caps");
    }
}
