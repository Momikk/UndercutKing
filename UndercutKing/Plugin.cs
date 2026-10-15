using Dalamud.Game.Command;
using Dalamud.Game.Network.Structures;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace UndercutKing;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IMarketBoard MarketBoard { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    public Configuration Config { get; }
    internal MarketPrices Prices { get; }
    internal NativeReader Reader { get; }
    internal RetainerSnapshot? Active { get; private set; }
    internal IReadOnlySet<uint> BagItems { get; private set; } = new HashSet<uint>();
    private readonly Highlighter highlighter;
    private readonly BagOverlay bagOverlay;
    private readonly WindowSystem windows = new("UndercutKing");
    private readonly MainWindow main;
    private readonly DtrCounter dtrCounter;
    private DateTimeOffset nextTick;
    private DateTimeOffset nextSave;
    private ulong characterId;
    private bool dirty;
    private bool disposed;

    public Plugin()
    {
        Config = Interface.GetPluginConfig() as Configuration ?? new();
        if (Config.Version < 2)
        {
            Config.FreshMinutes = 2 * 24 * 60;
            Config.Version = 2;
            Interface.SavePluginConfig(Config);
        }
        Config.FreshMinutes = Math.Clamp(Config.FreshMinutes, 1, 30 * 24 * 60);
        if (Config.Version < 3)
        {
            if (Config.AutomaticRefreshMinutes == 5) Config.AutomaticRefreshMinutes = 15;
            Config.Version = 3;
            Interface.SavePluginConfig(Config);
        }
        Config.AutomaticRefreshMinutes = Math.Clamp(Config.AutomaticRefreshMinutes, 1, 60);
        if (!Enum.IsDefined(Config.HighlightTheme)) Config.HighlightTheme = HighlightTheme.Auto;
        Prices = new();
        Reader = new(Prices);
        highlighter = new(this);
        main = new(this);
        dtrCounter = new(this, Open);
        windows.AddWindow(main);
        Commands.AddHandler("/undercutking", new CommandInfo(OnCommand) { HelpMessage = "打开压价之王设置与价格状态。" });
        Commands.AddHandler("/uking", new CommandInfo(OnCommand) { HelpMessage = "打开压价之王。" });
        Interface.UiBuilder.Draw += windows.Draw;
        bagOverlay = new(this);
        Interface.UiBuilder.OpenMainUi += Open;
        Interface.UiBuilder.OpenConfigUi += Open;
        MarketBoard.OfferingsReceived += Receive;
        Framework.Update += Tick;
        Log.Information("UndercutKing loaded: inventory hook={Inventory}, market hook={Market}", Reader.InventoryHookReady, Reader.MarketHookReady);
    }

    private void Open() => main.IsOpen = true;
    private void OnCommand(string command, string args) => main.Toggle();
    private void Receive(IMarketBoardCurrentOfferings offerings) => Prices.Receive(offerings);
    internal HighlightStatus Status(RetainerSnapshot snapshot, Listing listing) => PriceRules.Evaluate(listing,
        Prices.Get(snapshot.WorldId, listing.ItemId), DateTimeOffset.UtcNow, Config.FreshMinutes);
    internal void Save() { Interface.SavePluginConfig(Config); dirty = false; }

    internal void Refresh()
    {
        foreach (var snapshot in Config.Retainers.Where(x => x.CharacterId == Player.ContentId))
            foreach (var item in snapshot.Listings.Select(x => x.ItemId).Distinct())
                Prices.Queue(snapshot.WorldId, item, true, Config.AutomaticRefreshMinutes);
    }

    private void Tick(IFramework _)
    {
        if (disposed || DateTimeOffset.UtcNow < nextTick) return;
        nextTick = DateTimeOffset.UtcNow.AddMilliseconds(250);
        try
        {
            var id = Player.ContentId;
            if (id != characterId)
            {
                highlighter.Restore();
                Reader.Reset();
                Active = null;
                BagItems = new HashSet<uint>();
                characterId = id;
            }
            if (id == 0) { Active = null; Prices.Watch([], false); dtrCounter.Update(); return; }
            var owners = Reader.Owners();
            Prices.SetOwners(owners.Select(x => x.Id).Concat(Config.Retainers.Where(x => x.CharacterId == id).Select(x => x.RetainerId)),
                owners.Select(x => x.Name).Concat(Config.Retainers.Where(x => x.CharacterId == id).Select(x => x.Name)));
            var captured = Reader.Capture(id);
            Active = captured; // no use of a previous retainer's snapshot while loading
            BagItems = Config.BagMatchHighlight && (captured != null || NativeReader.RetainerListVisible())
                ? Reader.BagItems() ?? new HashSet<uint>() : new HashSet<uint>();
            if (captured != null)
            {
                var old = Config.Retainers.FirstOrDefault(x => x.CharacterId == id && x.RetainerId == captured.RetainerId);
                if (old == null || !old.Listings.SequenceEqual(captured.Listings) || old.Name != captured.Name || old.WorldId != captured.WorldId)
                {
                    if (old != null) Config.Retainers.Remove(old);
                    Config.Retainers.Add(captured);
                    dirty = true;
                }
            }
            Prices.Watch(Config.Retainers.Where(x => x.CharacterId == id)
                .SelectMany(x => x.Listings.Select(item => (x.WorldId, item.ItemId))), Config.AutomaticPriceCheck && Config.UseMarketPush, Config.AutomaticPriceCheck);
            if (Config.AutomaticPriceCheck)
                foreach (var snapshot in Config.Retainers.Where(x => x.CharacterId == id))
                    foreach (var item in snapshot.Listings.Select(x => x.ItemId).Distinct())
                        Prices.Queue(snapshot.WorldId, item, false, Prices.PushConnected ? Config.AutomaticRefreshMinutes : Math.Min(5, Config.AutomaticRefreshMinutes));
            if (dirty && DateTimeOffset.UtcNow >= nextSave)
            {
                Save();
                nextSave = DateTimeOffset.UtcNow.AddSeconds(5);
            }
            dtrCounter.Update();
        }
        catch (Exception ex)
        {
            Active = null;
            BagItems = new HashSet<uint>();
            nextTick = DateTimeOffset.UtcNow.AddSeconds(10);
            Log.Error(ex, "UndercutKing snapshot update failed");
        }
    }

    public void Dispose()
    {
        disposed = true;
        Framework.Update -= Tick;
        MarketBoard.OfferingsReceived -= Receive;
        dtrCounter.Dispose();
        bagOverlay.Dispose();
        highlighter.Dispose();
        Reader.Dispose();
        Prices.Dispose();
        Interface.UiBuilder.Draw -= windows.Draw;
        Interface.UiBuilder.OpenMainUi -= Open;
        Interface.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/undercutking");
        Commands.RemoveHandler("/uking");
        windows.RemoveAllWindows();
        Save();
    }
}
