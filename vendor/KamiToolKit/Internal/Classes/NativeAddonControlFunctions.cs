using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KamiToolKit.Internal.Classes;

/// <summary>
///     Native entry points used to manage child addons until these functions are available in ClientStructs.
/// </summary>
// TODO: FFCS
internal unsafe class NativeAddonControlFunctions
{
    /// <summary>
    ///     Constructs a native controller.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, AtkAddonControl*>                                                           Construct;
    /// <summary>
    ///     Destroys a controller, optionally releasing its allocation according to the free flags.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, byte, AtkAddonControl*>                                                     Destroy;
    /// <summary>
    ///     Links a controller to its parent addon.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, AtkUnitBase*, byte>                                                         Initialize;
    /// <summary>
    ///     Updates child setup, positions, and pending attachment.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, float, void>                                                             Update;
    /// <summary>
    ///     Draws ready attached children.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, void>                                                                       Draw;
    /// <summary>
    ///     Registers an allocated child by id; parameters include tab index, group id, an unused integer, and initial flags.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, ushort, byte, byte, int, int, int, byte, byte, byte, NativeChildAddonInfo*> RegisterChild;
    /// <summary>
    ///     Removes a child registration and closes its addon.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, AtkUnitBase*, void>                                                         RemoveChild;
    /// <summary>
    ///     Shows registered children with the supplied visibility flags.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, uint, void>                                                                 ShowChildren;
    /// <summary>
    ///     Hides registered children with callback, transition, and visibility settings.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, byte, byte, uint, void>                                                     HideChildren;
    /// <summary>
    ///     Restores controller-driven child update and draw.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, void>                                                Attach;
    /// <summary>
    ///     Restores independent child update and draw while retaining its registration.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, void>                                                Detach;
    /// <summary>
    ///     Registers a ButtonPress listener on a child drag handle.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, AtkResNode*, AtkEvent*>                              BindDragHandle;
    /// <summary>
    ///     Selects a tab and activates its group, returning the packed tab and child ids.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, int, uint>                                                                  SelectTab;
    /// <summary>
    ///     Activates a child group with optional deactivation and focus.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, int, byte, byte, void>                                                      ActivateGroup;
    /// <summary>
    ///     Unregisters global drag listeners without clearing controller drag pointers.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, void>                                                                       CancelDrag;
    /// <summary>
    ///     Applies addon scale through the native scale helper.
    /// </summary>
    internal readonly delegate* unmanaged<AtkUnitBase*, float, byte>                                                                    SetScale;
    /// <summary>
    ///     Dispatches an event through the controller.
    /// </summary>
    internal readonly delegate* unmanaged<AtkAddonControl*, AtkEventDispatcher.Event*, byte>                                            DispatchEvent;

    /// <summary>
    ///     Resolves the native child addon entry points from the loaded client.
    /// </summary>
    internal NativeAddonControlFunctions()
    {
        var scanner = ISigScanner.Get();

        Construct = (delegate* unmanaged<AtkAddonControl*, AtkAddonControl*>)scanner.ScanText
        (
            "48 89 5C 24 ?? 57 48 83 EC ?? 33 FF 48 8D 05 ?? ?? ?? ?? 48 89 01 48 8B D9 48 8D 05 ?? ?? ?? ?? 48 89 79 ?? 48 89 41 ?? 33 D2 48 89 79 ?? 41 B8 ?? ?? ?? ?? 8D 4F"
        );
        Destroy = (delegate* unmanaged<AtkAddonControl*, byte, AtkAddonControl*>)scanner.ScanText
            ("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8D 05 ?? ?? ?? ?? 8B EA 48 89 01 48 8B F9");
        Initialize = (delegate* unmanaged<AtkAddonControl*, AtkUnitBase*, byte>)scanner.ScanText("48 89 51 ?? 48 85 D2 74 ?? 83 8A");
        Update     = (delegate* unmanaged<AtkAddonControl*, float, void>)scanner.ScanText("40 53 55 57 41 57 48 81 EC ?? ?? ?? ?? 48 8B 79");
        Draw = (delegate* unmanaged<AtkAddonControl*, void>)scanner.ScanText
            ("48 89 5C 24 ?? 48 89 6C 24 ?? 57 48 83 EC ?? 48 8B 79 ?? 48 8B E9 48 8B 1F 48 3B DF 0F 84");
        RegisterChild = (delegate* unmanaged<AtkAddonControl*, ushort, byte, byte, int, int, int, byte, byte, byte, NativeChildAddonInfo*>)scanner.ScanText
            ("48 89 5C 24 ?? 48 89 6C 24 ?? 56 41 54 41 55 41 56 41 57 48 81 EC");
        RemoveChild = (delegate* unmanaged<AtkAddonControl*, AtkUnitBase*, void>)scanner.ScanText
            ("48 83 EC ?? 4C 8B 41 ?? 49 8B 00 49 3B C0 74 ?? 4C 8B 50 ?? 49 39 52");
        ShowChildren = (delegate* unmanaged<AtkAddonControl*, uint, void>)scanner.ScanText("48 89 5C 24 ?? 57 41 56 41 57 48 83 EC ?? 48 8B 79");
        HideChildren = (delegate* unmanaged<AtkAddonControl*, byte, byte, uint, void>)scanner.ScanText
            ("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 48 8B 79 ?? 41 8B E9");
        Attach = (delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, void>)scanner.ScanText("48 85 D2 74 ?? 48 8B 42 ?? 48 85 C0 74 ?? 80 4A");
        Detach = (delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, void>)scanner.ScanText
        (
            "48 85 D2 74 ?? 48 8B 42 ?? 48 85 C0 74 ?? 80 62 ?? ?? 80 88 ?? ?? ?? ?? ?? 48 8B 42 ?? 80 88 ?? ?? ?? ?? ?? 48 8B 42 ?? 48 85 C0 74 ?? 81 88 ?? ?? ?? ?? ?? ?? ?? ?? 81 A0 ?? ?? ?? ?? ?? ?? ?? ?? 48 8B 41"
        );
        BindDragHandle = (delegate* unmanaged<AtkAddonControl*, NativeChildAddonInfo*, AtkResNode*, AtkEvent*>)scanner.ScanText("48 83 EC ?? 4D 8B D0 4D 85 C0");
        SelectTab      = (delegate* unmanaged<AtkAddonControl*, int, uint>)scanner.ScanText("40 55 57 41 54 48 83 EC ?? 48 8B 79");
        ActivateGroup = (delegate* unmanaged<AtkAddonControl*, int, byte, byte, void>)scanner.ScanText
            ("48 89 5C 24 ?? 48 89 7C 24 ?? 41 54 41 56 41 57 48 83 EC ?? 48 8B 79");
        CancelDrag    = (delegate* unmanaged<AtkAddonControl*, void>)scanner.ScanText("48 89 5C 24 ?? 57 48 83 EC ?? 33 C0 4C 8D 49");
        SetScale      = (delegate* unmanaged<AtkUnitBase*, float, byte>)scanner.ScanText("48 83 EC ?? 8B 91 ?? ?? ?? ?? 8B C2");
        DispatchEvent = (delegate* unmanaged<AtkAddonControl*, AtkEventDispatcher.Event*, byte>)scanner.ScanText
        (
            "48 83 EC ?? 48 8B 41 ?? 4C 8D 49 ?? 48 85 C0 74 ?? 44 0F B7 02 0F B6 48 ?? 41 3B C8 74 ?? 48 8B 40 ?? 48 85 C0 75 ?? 33 C0 48 83 C4 ?? C3 45 33 C0 49 8B C9 E8 ?? ?? ?? ?? 84 C0 74 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? C3 CC CC CC CC CC CC CC CC CC 48 85 D2"
        );
    }
}
