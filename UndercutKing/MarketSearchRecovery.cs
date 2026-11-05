// Recovery behavior researched from DailyRoutines AutoRefreshMarketSearchResult
// and AtmoOmen/OmenTools GameState. See NOTICE.md. No wait-message memory patch.
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;

namespace UndercutKing;

internal sealed unsafe class MarketSearchRecovery : IDisposable
{
    private readonly Configuration config;
    private readonly NativeReader reader;
    private readonly MarketSearchRetry retries = new();
    private long nextTick;

    public MarketSearchRecovery(Configuration config, NativeReader reader)
    {
        this.config = config;
        this.reader = reader;
        reader.MarketResult += Observe;
        Plugin.Framework.Update += Tick;
    }

    private static nint Window()
    {
        var addon = Plugin.GameGui.GetAddonByName("ItemSearchResult");
        return !addon.IsNull && addon.IsReady && addon.IsVisible ? addon.Address : 0;
    }

    private bool Eligible(uint item) => config.AutoRetryMarketSearch && reader.MarketHookReady &&
        Plugin.Player.ContentId != 0 && !Plugin.Conditions[ConditionFlag.OnFreeTrial] &&
        !Plugin.Conditions[ConditionFlag.BoundByDuty] &&
        Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(item) is { } row && row.ItemSearchCategory.RowId > 0;

    private void Observe(uint status, uint count, uint item)
    {
        // Called after the existing packet handler has finished processing the result.
        // Only immutable identifiers enter the retry queue; never retain a native pointer.
        try
        {
            if (!Eligible(item)) { retries.Reset(); return; }
            var world = Plugin.Objects.LocalPlayer?.CurrentWorld.RowId ?? 0;
            retries.Observe(Plugin.Player.ContentId, world, item, Window(), status, count, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) { retries.Reset(); Plugin.Log.Error(ex, "Could not schedule market search recovery"); }
    }

    private void Tick(IFramework _)
    {
        if (Environment.TickCount64 < nextTick) return;
        nextTick = Environment.TickCount64 + 100;
        try
        {
            var info = InfoProxyItemSearch.Instance();
            var item = info == null ? 0 : info->SearchItemId;
            var world = Plugin.Objects.LocalPlayer?.CurrentWorld.RowId ?? 0;
            if (retries.Take(Plugin.Player.ContentId, world, item, Window(), Eligible(item), DateTimeOffset.UtcNow) && info != null)
            {
                Plugin.Log.Debug("Retrying rejected market search for item {Item}", item);
                info->RequestData();
            }
        }
        catch (Exception ex) { retries.Reset(); Plugin.Log.Error(ex, "Market search recovery stopped"); }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Tick;
        reader.MarketResult -= Observe;
        retries.Reset();
    }
}
