// ReSharper disable RedundantUnsafeContext

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using KamiToolKit.Controllers;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit;

/// <summary>
///     Warning, anything in this class is subject to change at any time.
///     This is mostly a staging platform for features that haven't made it into live ClientStructs.
///     These are not intended for external use, other than for experimenting.
/// </summary>
/// TODO: FFCS
public unsafe class Experimental
{
    /// <summary>
    ///     Gets the resolved native child addon entry points for this library instance.
    /// </summary>
    internal NativeAddonControlFunctions AddonControlFunctions
        => field ??= new NativeAddonControlFunctions();

    /// <summary>
    ///     Gets the controllers owned by this library instance for automatic disposal during unload.
    /// </summary>
    internal List<NativeAddonController> AddonControllers { get; } = [];
}
