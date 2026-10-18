using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KamiToolKit.Internal.Classes;

internal class EventHandlerInfo
{
    public Action?                                  OnActionDelegate;
    public AtkEventListener.Delegates.ReceiveEvent? OnReceiveEventDelegate;
}
