using System.Runtime.InteropServices;

namespace KamiToolKit.Internal.Classes;

/// <summary>
///     Partial layout of the native controller fields used for selection and drag state.
/// </summary>
// TODO: FFCS
[StructLayout(LayoutKind.Explicit, Size = 0x60)]
internal unsafe struct NativeAddonControlState
{
    /// <summary>
    ///     Index of the child selected through its tab index, or -1 before selection.
    /// </summary>
    [FieldOffset(0x40)]
    internal int SelectedIndex;
    /// <summary>
    ///     Child being tracked for drag detection or an attachment event.
    /// </summary>
    [FieldOffset(0x48)]
    internal NativeChildAddonInfo* DraggingChild;
    /// <summary>
    ///     Child waiting for its hide transition before attachment.
    /// </summary>
    [FieldOffset(0x50)]
    internal NativeChildAddonInfo* AttachingChild;
}
