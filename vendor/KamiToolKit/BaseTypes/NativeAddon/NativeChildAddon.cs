using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Nodes;

namespace KamiToolKit.BaseTypes;

/// <summary>
///     A native addon whose attachment and lifecycle are managed by a parent addon controller.
/// </summary>
/// <remarks>
///     The controller owns registered children. Operations that change native state must run on the game thread.
///     HostId establishes the native attachment. Callback routing is configured separately through ParentAddonId.
/// </remarks>
public class NativeChildAddon : NativeAddon
{
    private TaskCompletionSource openCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool                 attached       = true;
    private CollisionNode?       dragHandle;
    private NodeBase?            attachmentHiddenNode;
    private Vector2              localPosition;
    private bool                 headerCollisionNodeCaptured;
    private bool                 isDisposed;

    /// <summary>
    ///     Gets the header collision node captured after native child setup.
    /// </summary>
    internal unsafe AtkCollisionNode* OriginalHeaderCollisionNode { get; private set; }

    /// <summary>
    ///     Gets or sets the native registration for the current addon allocation.
    /// </summary>
    internal unsafe NativeChildAddonInfo* Info { get; set; }

    /// <summary>
    ///     Gets or sets whether the current addon allocation has started closing.
    /// </summary>
    internal bool IsClosing { get; set; }

    /// <summary>
    ///     Gets whether the child should be allocated when its parent is available.
    /// </summary>
    /// <remarks>
    ///     This remains true while a requested child waits for parent setup or for a previous allocation to finish closing.
    /// </remarks>
    public bool IsRequestedOpen { get; internal set; }

    /// <summary>
    ///     Gets whether the child should be visible when its parent is shown.
    /// </summary>
    internal bool IsShown { get; private set; } = true;

    /// <summary>
    ///     Gets or sets the screen position used when the child is detached.
    /// </summary>
    internal Vector2 DetachedPosition { get; set; }

    /// <summary>
    ///     Gets the controller that owns this child addon.
    /// </summary>
    public NativeAddonController Controller { get; }

    /// <summary>
    ///     Gets whether the child follows its parent through the native controller.
    /// </summary>
    public unsafe bool IsAttached => Info is null ?
                                         attached :
                                         (Info->Flags1 & 8) != 0;

    /// <summary>
    ///     Gets whether the native registration currently marks the child active.
    /// </summary>
    public unsafe bool IsActive => Info is not null && (Info->Flags2 & 2) != 0;

    /// <summary>
    ///     Gets or sets the offset from the parent root in unscaled child coordinates.
    /// </summary>
    /// <remarks>
    ///     The native controller multiplies these coordinates by the child scale. Values must fit in a signed 16-bit integer.
    /// </remarks>
    public unsafe Vector2 LocalPosition
    {
        get => Info is null ?
                   localPosition :
                   new Vector2(Info->PositionX, Info->PositionY);
        set
        {
            Controller.EnsureAccess();
            localPosition = new Vector2(checked((short)value.X), checked((short)value.Y));

            if (Info is not null)
            {
                Info->PositionX = (short)localPosition.X;
                Info->PositionY = (short)localPosition.Y;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the group activated together with this child.
    /// </summary>
    public unsafe int GroupID
    {
        get;
        set
        {
            Controller.EnsureAccess();
            field = value;
            if (Info is not null)
                Info->GroupID = value;
        }
    }

    /// <summary>
    ///     Gets or sets the index used to select this child through its controller.
    /// </summary>
    public unsafe int TabIndex
    {
        get;
        set
        {
            Controller.EnsureAccess();
            field = value;
            if (Info is not null)
                Info->TabIndex = value;
        }
    }

    /// <summary>
    ///     Gets or sets whether dragging the child can detach it from its parent.
    /// </summary>
    public unsafe bool AllowDetach
    {
        get;
        set
        {
            Controller.EnsureAccess();
            field = value;
            if (Info is not null)
                Info->Flags1 = (byte)((Info->Flags1 & ~4) |
                                      (value ?
                                           4 :
                                           0));
        }
    }

    /// <summary>
    ///     Gets or sets the collision node used to start drag detection.
    /// </summary>
    /// <remarks>
    ///     When null, the controller uses the original window header collision node captured after setup.
    /// </remarks>
    public unsafe CollisionNode? DragHandle
    {
        get => dragHandle;
        set
        {
            Controller.EnsureAccess();
            dragHandle = value;
            Controller.UpdateDragHandle(this);
        }
    }

    /// <summary>
    ///     Gets or sets a node hidden while attached and shown while detached.
    /// </summary>
    public unsafe NodeBase? AttachmentHiddenNode
    {
        get => attachmentHiddenNode;
        set
        {
            Controller.EnsureAccess();
            attachmentHiddenNode = value;
            Controller.UpdateAttachmentHiddenNode(this);
        }
    }

    /// <summary>
    ///     Gets or inits whether the child group is active when registered.
    /// </summary>
    public bool InitiallyActive { get; init; } = true;

    /// <summary>
    ///     Occurs when attachment changes; the argument is true when attached.
    /// </summary>
    public event Action<bool>? AttachmentChanged;

    /// <summary>
    ///     Disables independent position and scale configuration for child addons.
    /// </summary>
    protected override bool UsesWindowConfiguration => false;

    /// <summary>
    ///     Keeps the native allocation alive when the parent hides the child.
    /// </summary>
    protected override bool CloseOnHide => false;

    /// <summary>
    ///     Registers a child whose internal name will be supplied by a derived constructor or initializer.
    /// </summary>
    /// <remarks>
    ///     The name must be set before opening the child. The controller manages its lifetime.
    /// </remarks>
    protected NativeChildAddon
    (
        NativeAddonController controller
    )
    {
        Controller              = controller;
        Title                   = string.Empty;
        RememberClosePosition   = false;
        OpenInBounds            = false;
        EnableContextMenu       = false;
        OpenWindowSoundEffectId = 0;
        controller.Register(this);
    }

    /// <summary>
    ///     Registers a child addon with the specified controller and internal name.
    /// </summary>
    [SetsRequiredMembers]
    public NativeChildAddon
    (
        NativeAddonController controller,
        string                internalName
    ) : this(controller) => InternalName = internalName;

    /// <summary>
    ///     Requests allocation and native registration when the parent is ready.
    /// </summary>
    /// <remarks>
    ///     A request made before parent setup is retained until the parent becomes available.
    /// </remarks>
    public override void Open()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Controller.EnsureAccess();
        if (!IsRequestedOpen)
            openCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        IsRequestedOpen = true;
        Controller.OpenChild(this);
    }

    /// <summary>
    ///     Requests opening on the game thread and waits for native child setup to complete.
    /// </summary>
    /// <remarks>
    ///     Closing or disposing the child cancels the pending wait.
    /// </remarks>
    public override async Task OpenAsync()
    {
        await IFramework.Get().Run(Open);
        await openCompletion.Task;
    }

    /// <summary>
    ///     Cancels the open request and removes the current native child allocation.
    /// </summary>
    /// <remarks>
    ///     The managed instance remains registered and may be opened again.
    /// </remarks>
    public override void Close()
    {
        CancelOpening();
        Controller.CloseChild(this);
    }

    /// <summary>
    ///     Toggles the requested open state, including requests waiting for parent setup.
    /// </summary>
    public override void Toggle()
    {
        if (IsRequestedOpen)
            Close();
        else
            Open();
    }

    /// <summary>
    ///     Allows the child to become visible when its parent is shown.
    /// </summary>
    public void Show()
    {
        Controller.EnsureAccess();
        IsShown = true;
        Controller.UpdateVisibility(this);
    }

    /// <summary>
    ///     Hides the child without closing its native allocation.
    /// </summary>
    public void Hide()
    {
        Controller.EnsureAccess();
        IsShown = false;
        Controller.UpdateVisibility(this);
    }

    /// <summary>
    ///     Attaches the child so its parent controller updates its position and draws it.
    /// </summary>
    public void Attach()
    {
        Controller.EnsureAccess();
        Controller.SetAttached(this, true);
    }

    /// <summary>
    ///     Detaches the child while keeping its registration with the parent controller.
    /// </summary>
    public void Detach()
    {
        Controller.EnsureAccess();
        Controller.SetAttached(this, false);
    }

    /// <summary>
    ///     Unregisters the child and releases its native addon allocation.
    /// </summary>
    public override void Dispose()
    {
        if (isDisposed)
            return;

        if (!ThreadSafety.IsMainThread)
            throw new InvalidOperationException("Native addon operations must run on the game thread.");

        isDisposed = true;
        Controller.Forget(this);
        base.Dispose();
    }

    /// <summary>
    ///     Unregisters the child on the game thread and waits for its addon to close.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await IFramework.Get().Run
        (() =>
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                Controller.Forget(this);
            }
        );
        await base.DisposeAsync();
    }

    /// <summary>
    ///     Sets the screen position and updates the corresponding attached offset or detached position.
    /// </summary>
    public override unsafe void SetWindowPosition
    (
        Vector2 windowPosition
    )
    {
        Controller.EnsureAccess();
        var parent = Controller.ParentAddon;

        if (IsAttached && InternalAddon is not null && parent is not null)
        {
            var parentPosition = parent->RootNode is null ?
                                     new Vector2(parent->X,           parent->Y) :
                                     new Vector2(parent->RootNode->X, parent->RootNode->Y);
            LocalPosition = (windowPosition - parentPosition) / InternalAddon->Scale;
        }
        else
            DetachedPosition = windowPosition;

        base.SetWindowPosition(windowPosition);
    }

    /// <summary>
    ///     Starts closing an allocated addon that no longer has a native registration.
    /// </summary>
    internal unsafe void CloseAllocatedAddon()
    {
        if (InternalAddon is null || IsClosing)
            return;

        IsClosing = true;
        InternalAddon->Close(false);
    }

    /// <summary>
    ///     Stores attachment state and raises the attachment event when it changes.
    /// </summary>
    internal void RememberAttachment
    (
        bool value
    )
    {
        if (attached == value)
            return;

        attached = value;
        AttachmentChanged?.Invoke(value);
    }

    /// <summary>
    ///     Reads native attachment state and completes opening once setup finishes.
    /// </summary>
    internal unsafe void SynchronizeState()
    {
        if (Info is null)
            return;

        var currentAttached = (Info->Flags1 & 8) != 0;
        var changed         = attached           != currentAttached;
        attached = currentAttached;
        if (!attached && InternalAddon is not null)
            DetachedPosition = new Vector2(InternalAddon->X, InternalAddon->Y);

        if ((Info->Flags1 & 1) != 0)
            openCompletion.TrySetResult();

        if (changed)
            AttachmentChanged?.Invoke(attached);
    }

    /// <summary>
    ///     Retains the open request and attached position while the parent destroys native child registrations.
    /// </summary>
    internal unsafe void ParentClosing()
    {
        SynchronizeState();
        if (Info is not null)
            localPosition = new Vector2(Info->PositionX, Info->PositionY);

        Info      = null;
        IsClosing = IsAllocated;
        if (openCompletion.Task.IsCompleted && IsRequestedOpen)
            openCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    ///     Clears references to the finalized native registration and its nodes.
    /// </summary>
    internal unsafe void Released()
    {
        Info                        = null;
        dragHandle                  = null;
        attachmentHiddenNode        = null;
        OriginalHeaderCollisionNode = null;
        headerCollisionNodeCaptured = false;
    }

    /// <summary>
    ///     Clears the open request and cancels any pending setup wait.
    /// </summary>
    internal void CancelOpening()
    {
        IsRequestedOpen = false;
        openCompletion.TrySetCanceled();
    }

    /// <summary>
    ///     Captures the original header collision node once for the current allocation.
    /// </summary>
    internal unsafe void CaptureHeaderCollisionNode()
    {
        if (headerCollisionNodeCaptured)
            return;

        OriginalHeaderCollisionNode = InternalAddon->WindowHeaderCollisionNode;
        headerCollisionNodeCaptured = true;
    }

    /// <summary>
    ///     Checks the child blocking count and the parent input state.
    /// </summary>
    internal unsafe bool ShouldIgnoreInputs
    (
        AtkUnitBase* addon
    )
    {
        var parent = Controller.ParentAddon;
        return addon->NumBlockingAddons != 0 || parent is null || parent->ShouldIgnoreInputs();
    }

    /// <summary>
    ///     Forwards back button input to the parent while attached.
    /// </summary>
    internal unsafe bool HandleBackButtonInput
    (
        AtkUnitBase* addon,
        int          inputId,
        bool         repeat
    )
    {
        var parent = Controller.ParentAddon;
        return IsAttached && parent is not null && parent->HandleBackButtonInput(inputId, repeat);
    }
}
