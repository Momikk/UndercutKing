using Dalamud.Plugin.Ipc;

namespace UndercutKing;

internal sealed class RecycleKingCompatibility
{
    private readonly ICallGateSubscriber<uint[]> highlighted = Plugin.Interface.GetIpcSubscriber<uint[]>("RecycleKing.GetHighlightedItemIds");
    private long retryAfter;

    internal uint[] Targets()
    {
        if (Environment.TickCount64 < retryAfter) return [];
        if (!Plugin.Interface.InstalledPlugins.Any(p => p.InternalName == "RecycleKing" && p.IsLoaded)) return [];
        try { return highlighted.InvokeFunc() ?? []; }
        catch
        {
            // Optional peer may still be loading, or may predate its IPC. Keep normal blue highlighting.
            retryAfter = Environment.TickCount64 + 1000;
            return [];
        }
    }
}
