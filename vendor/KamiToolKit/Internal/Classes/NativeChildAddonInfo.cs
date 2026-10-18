using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KamiToolKit.Internal.Classes;

/// <summary>
///     Native registration and attachment state of a child addon.
/// </summary>
// TODO: FFCS
[StructLayout(LayoutKind.Explicit, Size = 0x48)]
internal unsafe struct NativeChildAddonInfo
{
    /// <summary>
    ///     Addon allocation associated with the registration.
    /// </summary>
    [FieldOffset(0x08)]
    internal AtkUnitBase* Addon;
    /// <summary>
    ///     Node whose ButtonPress event starts drag detection.
    /// </summary>
    [FieldOffset(0x10)]
    internal AtkResNode* DragHandle;
    /// <summary>
    ///     Node hidden while attached and restored when detached.
    /// </summary>
    [FieldOffset(0x18)]
    internal AtkResNode* AttachmentHiddenNode;
    /// <summary>
    ///     Collision node used to detect overlap with the parent header.
    /// </summary>
    [FieldOffset(0x20)]
    internal AtkCollisionNode* CollisionNode;
    /// <summary>
    ///     Id of the registered child addon.
    /// </summary>
    [FieldOffset(0x28)]
    internal ushort AddonID;
    /// <summary>
    ///     Group activated together with this child.
    /// </summary>
    [FieldOffset(0x2C)]
    internal int GroupID;
    /// <summary>
    ///     Index used to select this child through the native controller.
    /// </summary>
    [FieldOffset(0x30)]
    internal int TabIndex;
    /// <summary>
    ///     Stored scale value initialized to 1 during registration.
    /// </summary>
    [FieldOffset(0x34)]
    internal float Scale;
    /// <summary>
    ///     Horizontal offset from the parent root, multiplied by the child scale.
    /// </summary>
    [FieldOffset(0x3C)]
    internal short PositionX;
    /// <summary>
    ///     Vertical offset from the parent root, multiplied by the child scale.
    /// </summary>
    [FieldOffset(0x3E)]
    internal short PositionY;
    /// <summary>
    ///     Setup, group, detachment, and attachment flags.
    /// </summary>
    /// <remarks>
    ///     Bits 0 through 6 track completed setup, active group, permitted detachment, attachment, previous dragging, pending attachment, and initial attachment.
    /// </remarks>
    [FieldOffset(0x40)]
    internal byte Flags1;
    /// <summary>
    ///     Additional registration flags.
    /// </summary>
    /// <remarks>
    ///     Bit 1 indicates native active state. Bit 2 requests hiding the window node and focusing the child during initial attachment setup.
    /// </remarks>
    [FieldOffset(0x41)]
    internal byte Flags2;
}
