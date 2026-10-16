using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;

namespace UndercutKing;

internal sealed class BackgroundFrameLimiter : IDisposable
{
    private readonly Configuration config;
    private readonly FramePacer pacer = new(Stopwatch.GetTimestamp, Thread.Sleep, Stopwatch.Frequency);
    private readonly Func<bool> isForeground = IsForeground;

    internal BackgroundFrameLimiter(Configuration config)
    {
        this.config = config;
        Plugin.Framework.Update += Update;
    }

    private void Update(IFramework _) => pacer.Update(config.LimitBackgroundFps, config.BackgroundFps, isForeground);

    private static bool IsForeground()
    {
        var window = GetForegroundWindow();
        // Unknown focus fails open rather than throttling a possibly active game.
        if (window == 0 || GetWindowThreadProcessId(window, out var processId) == 0) return true;
        return processId == (uint)Environment.ProcessId;
    }

    public void Dispose() => Plugin.Framework.Update -= Update;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
