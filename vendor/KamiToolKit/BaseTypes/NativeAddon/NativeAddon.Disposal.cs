using Dalamud.Plugin.Services;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.BaseTypes;

public partial class NativeAddon : IDisposable, IAsyncDisposable
{
    private AddonDisposeState disposeState;

    internal static List<NativeAddon> CreatedAddons { get; } = [];

    /// <summary>
    ///     Triggers the disposal of this addon, and awaits for it to fully close before returning
    ///     <see cref="ValueTask.CompletedTask" />
    /// </summary>
    /// <remarks>
    ///     This <em>must not</em> be called from the main thread, or it will deadlock the game.
    /// </remarks>
    public virtual async ValueTask DisposeAsync()
    {
        if (disposeState is not AddonDisposeState.Alive) return;

        if (IsOverlayAddon)
        {
            RestoreVirtualTable();
            disposeState = AddonDisposeState.Disposed;
            CreatedAddons.Remove(this);
            GC.SuppressFinalize(this);
            return;
        }

        IPluginLog.Get().Verbose($"Disposing addon {GetType()}");

        disposeState = AddonDisposeState.Disposing;
        await CloseAsync();

        CreatedAddons.Remove(this);

        GC.SuppressFinalize(this);
        disposeState = AddonDisposeState.Disposed;
    }

    /// <summary>
    ///     Triggers the disposal of this addon. <br /> <br />
    ///     This will not await the addon to actually close which happens several frames later
    ///     due to the addons closing animation. If you need to fully wait for the window to close use
    ///     <see cref="DisposeAsync" /> and await the result.
    /// </summary>
    /// <code>await thisInstance.DisposeAsync();</code>
    public virtual unsafe void Dispose()
    {
        if (disposeState is not AddonDisposeState.Alive) return;

        if (IsOverlayAddon)
        {
            RestoreVirtualTable();
            disposeState = AddonDisposeState.Disposed;
            CreatedAddons.Remove(this);
            GC.SuppressFinalize(this);
            return;
        }

        IPluginLog.Get().Verbose($"Disposing addon {GetType()}");

        disposeState = AddonDisposeState.Disposing;
        if (InternalAddon is null)
            disposeState = AddonDisposeState.Disposed;
        else
            Close();

        CreatedAddons.Remove(this);

        GC.SuppressFinalize(this);
        // disposeState will be set to Disposed when the game calls Destructor
    }

    internal static void WarnLeakedAddons()
    {
        foreach (var addon in CreatedAddons.ToArray())
        {
            if (addon.IsOverlayAddon) continue;

            IPluginLog.Get().Warning($"Addon {addon.GetType()} was not disposed properly please ensure you call dispose at an appropriate time.");
            IPluginLog.Get().Debug($"Automatically disposing addon {addon.GetType()} as a safety measure.");
        }
    }

    internal static void DisposeAddons()
    {
        var addons = CreatedAddons.ToArray();

        foreach (var addon in addons)
        {
            if (addon.IsOverlayAddon) continue;

            try
            {
                addon.Dispose();
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        foreach (var addon in addons)
        {
            try
            {
                addon.RestoreVirtualTable();
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        CreatedAddons.Clear();
    }

    private enum AddonDisposeState : byte
    {
        Alive     = 0,
        Disposing = 1,
        Disposed  = 2
    }
}
