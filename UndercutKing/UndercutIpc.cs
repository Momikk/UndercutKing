namespace UndercutKing;

internal sealed class UndercutIpc : IDisposable
{
    private const string SnapshotEndpoint = "UndercutKing.GetSnapshot";
    private const string RefreshEndpoint = "UndercutKing.RequestRefresh";
    private const string RecoveryEndpoint = "UndercutKing.IsMarketSearchRecovering";
    private readonly Plugin plugin;
    private IpcSnapshot current = IpcSnapshot.Empty;
    private long requestedCharacter;

    public UndercutIpc(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Interface.GetIpcProvider<string>(SnapshotEndpoint).RegisterFunc(GetSnapshot);
        Plugin.Interface.GetIpcProvider<bool>(RefreshEndpoint).RegisterFunc(RequestRefresh);
        Plugin.Interface.GetIpcProvider<uint, bool>(RecoveryEndpoint).RegisterFunc(plugin.IsMarketSearchRecovering);
        Plugin.ClientState.Logout += OnLogout;
    }

    private string GetSnapshot() => Volatile.Read(ref current).ToJson();

    private bool RequestRefresh()
    {
        var snapshot = Volatile.Read(ref current);
        if (!snapshot.HasRefreshableListings) return false;
        Interlocked.Exchange(ref requestedCharacter, unchecked((long)snapshot.CharacterId));
        return true;
    }

    public bool TakeRefresh(ulong character) =>
        character != 0 && Interlocked.Exchange(ref requestedCharacter, 0) == unchecked((long)character);

    // Publish only detached data. IPC callers can run outside the framework thread.
    public void Publish(ulong character, IReadOnlyList<(ulong Id, string Name)> owners)
    {
        var snapshot = IpcSnapshot.Create(character, owners, plugin.Config.Retainers, plugin.Status,
            plugin.BagItems, plugin.Config.ItemHighlight, plugin.Config.BagMatchHighlight);
        Volatile.Write(ref current, snapshot);
    }

    public void Clear()
    {
        Volatile.Write(ref current, IpcSnapshot.Empty);
        Interlocked.Exchange(ref requestedCharacter, 0);
    }

    private void OnLogout(int type, int code) => Clear();

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        Clear();
        Plugin.Interface.GetIpcProvider<bool>(RefreshEndpoint).UnregisterFunc();
        Plugin.Interface.GetIpcProvider<uint, bool>(RecoveryEndpoint).UnregisterFunc();
        Plugin.Interface.GetIpcProvider<string>(SnapshotEndpoint).UnregisterFunc();
    }

}
