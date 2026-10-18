using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KamiToolKit.Internal.Classes;

/// <summary>
///     Wrappers for AtkUnitManager functions that FFXIVClientStructs does not declare.
/// </summary>
internal static unsafe class AtkUnitManagerFunctions
{
    // TODO: FFCS
    private const string SetAddonBlockingSignature = "66 85 D2 0F 84 ?? ?? ?? ?? 48 89 6C 24 ?? 56";

    private static SetAddonBlockingDelegate setAddonBlockingPtr = null!;

    /// <summary>
    ///     Resolves every signature used by this class.
    /// </summary>
    /// <remarks>
    ///     Must be invoked once during <see cref="KamiToolKitLibrary.InitializeAsync" />.
    /// </remarks>
    internal static void Initialize()
        => setAddonBlockingPtr = Marshal.GetDelegateForFunctionPointer<SetAddonBlockingDelegate>(ISigScanner.Get().ScanText(SetAddonBlockingSignature));

    /// <summary>
    ///     Adds or removes a blocking entry that the addon matching <paramref name="addonId" /> holds.
    /// </summary>
    /// <remarks>
    ///     The game resolves <paramref name="addonId" /> through its loaded unit list, so the target addon has to be
    ///     registered already.
    ///     Unblocking is performed by the game itself, see the BlockedParentId handling of AtkUnitBase.Hide.
    /// </remarks>
    internal static void SetAddonBlocking
    (
        AtkUnitManager* unitManager,
        ushort          addonId,
        bool            isBlocking
    )
        => setAddonBlockingPtr
        (
            unitManager,
            addonId,
            isBlocking ?
                (byte)1 :
                (byte)0
        );

    private delegate void SetAddonBlockingDelegate
    (
        AtkUnitManager* unitManager,
        ushort          addonId,
        byte            isBlocking
    );
}
