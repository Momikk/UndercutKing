using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon
{
    private static Hook<AtkUnitBase.Delegates.FireCallback>? fireCallbackHook;

    internal static void InitializeCloseCallback()
    {
        fireCallbackHook = IGameInteropProvider.Get().HookFromAddress<AtkUnitBase.Delegates.FireCallback>(AtkUnitBase.Addresses.FireCallback.Value, OnFireCallback);
        fireCallbackHook.Enable();
    }

    private static bool OnFireCallback
    (
        AtkUnitBase* thisPtr,
        uint         valueCount,
        AtkValue*    values,
        bool         close
    )
    {
        try
        {
            IPluginLog.Get().Excessive($"[{thisPtr->NameString}] OnFireCallback");

            foreach (var addon in CreatedAddons)
            {
                if (addon == thisPtr && close && addon is { RespectCloseAll: true, IsOverlayAddon: false })
                {
                    if (addon is NativeChildAddon { IsAttached: true } childAddon)
                    {
                        var parent = childAddon.Controller.ParentAddon;
                        if (parent is not null)
                            return parent->FireCallback(valueCount, values, true);
                    }

                    addon.Close();
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        return fireCallbackHook!.Original(thisPtr, valueCount, values, close);
    }

    internal static void DisposeCloseCallback()
    {
        fireCallbackHook?.Dispose();
        fireCallbackHook = null;
    }
}
