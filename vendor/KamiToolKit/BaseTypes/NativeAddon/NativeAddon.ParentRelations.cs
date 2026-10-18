using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon
{
    /// <summary>
    ///     Applies <see cref="ParentAddonId" /> and <see cref="BlockedParentAddonId" /> to the allocated addon.
    /// </summary>
    /// <remarks>
    ///     The game resolves the target addon through its loaded unit list, so this only runs once the own addon has an ID and
    ///     is registered.
    ///     The matching unblock happens in AtkUnitBase.Hide, which reads BlockedParentId and clears it afterwards.
    /// </remarks>
    private void ApplyParentRelations()
    {
        if (IsOverlayAddon) return;

        if (ParentAddonId is not 0)
            InternalAddon->ParentId = (ushort)ParentAddonId;

        if (BlockedParentAddonId is 0) return;

        InternalAddon->BlockedParentId = (ushort)BlockedParentAddonId;
        AtkUnitManagerFunctions.SetAddonBlocking((AtkUnitManager*)RaptureAtkUnitManager.Instance(), (ushort)BlockedParentAddonId, true);
    }
}
