using System.Numerics;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon
{
    internal Vector2 LastClosePosition = Vector2.Zero;

    /// <summary>
    ///     Gets or inits the addons internal name.
    /// </summary>
    /// <remarks>
    ///     Names are limited to 31 characters.
    /// </remarks>
    public required string InternalName
    {
        get;
        init => field = new string(value.Replace(" ", "").Take(31).ToArray());
    }

    /// <summary>
    ///     Gets or sets the addons main title string.
    /// </summary>
    public required ReadOnlySeString Title
    {
        get;
        set
        {
            field = value;
            // Title values survive Close(); native child nodes do not.
            if (InternalAddon is not null && !isFinalized)
                WindowNode?.SetTitle(value.ToString(), Subtitle?.ToString() ?? KamiToolKitLibrary.DefaultWindowSubtitle);
        }
    }

    /// <summary>
    ///     Gets or sets the addons subtitle string, defaults to <see cref="KamiToolKitLibrary.DefaultWindowSubtitle" /> set
    ///     via <see cref="KamiToolKitLibrary.InitializeAsync" />.
    /// </summary>
    /// <remarks>
    ///     It is recommended to only change this if your windows main title is already representative of your plugins name.
    /// </remarks>
    public ReadOnlySeString? Subtitle
    {
        get;
        set
        {
            field = value;
            if (InternalAddon is not null && !isFinalized)
                WindowNode?.SetTitle(Title.ToString(), value?.ToString() ?? KamiToolKitLibrary.DefaultWindowSubtitle);
        }
    }

    /// <summary>
    ///     Sound effect to play when opening or closing this addon.
    /// </summary>
    public int OpenWindowSoundEffectId
    {
        get;
        set
        {
            field = value;

            if (InternalAddon is not null)
            {
                InternalAddon->ShowSoundEffectId = (short)value;
            }
        }
    } = 23;

    /// <summary>
    ///     Gets or sets this addons size, defaults to 400px by 400px.
    /// </summary>
    public Vector2 Size
    {
        get;
        set
        {
            field = value;

            if (value == Vector2.Zero)
                field = new Vector2(400.0f, 400.0f);

            if (InternalAddon is not null)
            {
                WindowNode?.Size = field;

                if (WindowNode is null)
                    InternalAddon->SetSize((ushort)field.X, (ushort)field.Y);
                else
                    InternalAddon->Resize(field);
            }
        }
    } = new(400.0f, 400.0f);

    /// <summary>
    ///     Gets the position of the content body start.
    /// </summary>
    /// <remarks>
    ///     With a window node, this is the bottom left of the header plus horizontal padding.
    ///     Without a window node, this is <see cref="ContentPadding" />.
    /// </remarks>
    public virtual Vector2 ContentStartPosition
        => WindowNode is null ?
               ContentPadding :
               WindowNode.ContentStartPosition +
               ContentPadding with
               {
                   Y = 0.0f
               };

    /// <summary>
    ///     Gets the size of the body of the window.
    /// </summary>
    /// <remarks>
    ///     With a window node, the header and content padding are excluded.
    ///     Without a window node, padding is excluded on both sides.
    /// </remarks>
    public virtual Vector2 ContentSize
        => WindowNode is null ?
               Size - (ContentPadding * 2.0f) :
               WindowNode.ContentSize -
               ContentPadding with
               {
                   X = ContentPadding.X * 2.0f
               };

    /// <summary>
    ///     Gets or sets the padding used for the content area.
    /// </summary>
    public Vector2 ContentPadding { get; set; } = new(8.0f, 8.0f);

    /// <summary>
    ///     Gets or sets the depth layer this window will open on.
    /// </summary>
    public int DepthLayer { get; init; } = 5;

    /// <summary>
    ///     Gets whether this window is open and visible.
    /// </summary>
    public bool IsOpen
        => InternalAddon is not null && InternalAddon->IsVisible;

    /// <summary>
    ///     Gets this addons ID.
    /// </summary>
    public ushort AddonId
        => InternalAddon is null ?
               (ushort)0 :
               InternalAddon->Id;

    /// <summary>
    ///     Gets or inits the addons ID that this window reports to the game in its callbacks.
    /// </summary>
    /// <remarks>
    ///     AtkUnitBase.FireCallback passes AtkUnitBase.ParentId instead of the own ID, so setting this delegates the callback
    ///     to another addon.
    /// </remarks>
    public int ParentAddonId { get; init; }

    /// <summary>
    ///     Gets or inits the addons ID that this window blocks while it is shown.
    /// </summary>
    /// <remarks>
    ///     Setting this increments AtkUnitBase.NumBlockingAddons of the target addon, the game decrements it again when this
    ///     window hides.
    /// </remarks>
    public int BlockedParentAddonId { get; init; }

    /// <summary>
    ///     Gets or sets whether this addon should remove its close position.
    /// </summary>
    public bool RememberClosePosition { get; set; } = true;

    /// <summary>
    ///     Gets or sets if this addon should be forced into the viewable area when opening.
    /// </summary>
    public bool OpenInBounds { get; init; } = true;

    internal bool IsOverlayAddon { get; init; }

    /// <summary>
    ///     Gets or inits whether allocation creates a window node.
    /// </summary>
    /// <remarks>
    ///     When false, content starts at ContentPadding and its size is the addon size minus padding on both sides.
    /// </remarks>
    public bool HasWindowNode { get; init; } = true;

    /// <summary>
    ///     Gets whether a native allocation exists, including while hidden or closing.
    /// </summary>
    public bool IsAllocated => InternalAddon is not null;

    /// <summary>
    ///     Gets whether setup and hide load and save independent window position and scale configuration.
    /// </summary>
    protected virtual bool UsesWindowConfiguration => true;

    /// <summary>
    ///     Gets whether hiding the addon also starts closing its native allocation.
    /// </summary>
    protected virtual bool CloseOnHide => true;
}
