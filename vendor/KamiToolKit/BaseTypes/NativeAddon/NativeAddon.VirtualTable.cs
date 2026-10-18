using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Timelines;

namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon
{
    private AtkUnitBase.Delegates.Dtor               destructorFunction         = null!;
    private AtkUnitBase.Delegates.Draw               drawFunction               = null!;
    private AtkUnitBase.Delegates.Finalizer          finalizerFunction          = null!;
    private AtkUnitBase.Delegates.Hide               hideFunction               = null!;
    private AtkUnitBase.Delegates.Initialize         initializeFunction         = null!;
    private AtkUnitBase.Delegates.ReceiveGlobalEvent receiveGlobalEventFunction = null!;

    private AtkUnitBase.AtkUnitBaseVirtualTable*        modifiedVirtualTable;
    private AtkUnitBase.Delegates.OnRefresh             onRefreshFunction           = null!;
    private AtkUnitBase.Delegates.OnRequestedUpdate     onRequestedUpdateFunction   = null!;
    private AtkUnitBase.Delegates.OnScreenSizeChange    onScreenSizeChangedFunction = null!;
    private AtkUnitBase.Delegates.OnSetup               onSetupFunction             = null!;
    private AtkUnitBase.AtkUnitBaseVirtualTable*        originalVirtualTable;
    private AtkUnitBase.Delegates.Show                  showFunction                  = null!;
    private AtkUnitBase.Delegates.Hide2                 softHideFunction              = null!;
    private AtkUnitBase.Delegates.Update                updateFunction                = null!;
    private AtkUnitBase.Delegates.ShouldIgnoreInputs    shouldIgnoreInputsFunction    = null!;
    private AtkUnitBase.Delegates.HandleBackButtonInput handleBackButtonInputFunction = null!;

    private static void ClearTimelineManager
    (
        AtkUnitBase* addon
    ) =>
        addon->UldManager.TimelineManager = null;

    private void RegisterVirtualTable()
    {

        originalVirtualTable = InternalAddon->VirtualTable;

        // Overwrite virtual table with a custom copy,
        // Note: currently there are 73 vfuncs, but there's no harm in copying more for when they add new vfuncs to the game
        modifiedVirtualTable = (AtkUnitBase.AtkUnitBaseVirtualTable*)NativeMemoryHelper.Malloc(0x8 * VIRTUAL_TABLE_ENTRY_COUNT);
        NativeMemory.Copy(InternalAddon->VirtualTable, modifiedVirtualTable, 0x8 * VIRTUAL_TABLE_ENTRY_COUNT);
        InternalAddon->VirtualTable = modifiedVirtualTable;

        initializeFunction          = Initialize;
        onSetupFunction             = Setup;
        showFunction                = Show;
        updateFunction              = Update;
        drawFunction                = Draw;
        hideFunction                = Hide;
        softHideFunction            = Hide2;
        finalizerFunction           = Finalizer;
        destructorFunction          = Destructor;
        onRequestedUpdateFunction   = RequestedUpdate;
        onRefreshFunction           = Refresh;
        onScreenSizeChangedFunction = ScreenSizeChange;
        receiveGlobalEventFunction  = OnReceiveGlobalEvent;

        modifiedVirtualTable->Initialize = (delegate* unmanaged<AtkUnitBase*, void>)Marshal.GetFunctionPointerForDelegate(initializeFunction);
        modifiedVirtualTable->OnSetup    = (delegate* unmanaged<AtkUnitBase*, uint, AtkValue*, void>)Marshal.GetFunctionPointerForDelegate(onSetupFunction);
        modifiedVirtualTable->Show       = (delegate* unmanaged<AtkUnitBase*, bool, uint, void>)Marshal.GetFunctionPointerForDelegate(showFunction);
        modifiedVirtualTable->Update     = (delegate* unmanaged<AtkUnitBase*, float, void>)Marshal.GetFunctionPointerForDelegate(updateFunction);
        modifiedVirtualTable->Draw       = (delegate* unmanaged<AtkUnitBase*, void>)Marshal.GetFunctionPointerForDelegate(drawFunction);
        modifiedVirtualTable->Hide       = (delegate* unmanaged<AtkUnitBase*, bool, bool, uint, void>)Marshal.GetFunctionPointerForDelegate(hideFunction);
        modifiedVirtualTable->Hide2      = (delegate* unmanaged<AtkUnitBase*, void>)Marshal.GetFunctionPointerForDelegate(softHideFunction);
        modifiedVirtualTable->Finalizer  = (delegate* unmanaged<AtkUnitBase*, void>)Marshal.GetFunctionPointerForDelegate(finalizerFunction);
        modifiedVirtualTable->Dtor       = (delegate* unmanaged<AtkUnitBase*, byte, AtkEventListener*>)Marshal.GetFunctionPointerForDelegate(destructorFunction);
        modifiedVirtualTable->OnRequestedUpdate =
            (delegate* unmanaged<AtkUnitBase*, NumberArrayData**, StringArrayData**, void>)Marshal.GetFunctionPointerForDelegate(onRequestedUpdateFunction);
        modifiedVirtualTable->OnRefresh = (delegate* unmanaged<AtkUnitBase*, uint, AtkValue*, bool>)Marshal.GetFunctionPointerForDelegate(onRefreshFunction);
        modifiedVirtualTable->OnScreenSizeChange = (delegate* unmanaged<AtkUnitBase*, int, int, void>)Marshal.GetFunctionPointerForDelegate
            (onScreenSizeChangedFunction);
        modifiedVirtualTable->ReceiveGlobalEvent =
            (delegate* unmanaged<AtkUnitBase*, AtkEventType, int, AtkEvent*, AtkEventData*, void>)Marshal.GetFunctionPointerForDelegate(receiveGlobalEventFunction);

        if (this is NativeChildAddon childAddon)
        {
            shouldIgnoreInputsFunction               = childAddon.ShouldIgnoreInputs;
            handleBackButtonInputFunction            = childAddon.HandleBackButtonInput;
            modifiedVirtualTable->ShouldIgnoreInputs = (delegate* unmanaged<AtkUnitBase*, bool>)Marshal.GetFunctionPointerForDelegate(shouldIgnoreInputsFunction);
            modifiedVirtualTable->HandleBackButtonInput = (delegate* unmanaged<AtkUnitBase*, int, bool, bool>)Marshal.GetFunctionPointerForDelegate
                (handleBackButtonInputFunction);
        }
    }

    internal void RestoreVirtualTable()
    {
        if (InternalAddon is null) return;
        if (modifiedVirtualTable is null) return;

        if (RootNode.ResNode is not null)
        {
            var timeline = RootNode.Timeline;
            RootNode.Timeline          = null;
            RootNode.ResNode->Timeline = null;

            try
            {
                timeline?.Dispose();
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        ClearTimelineManager(InternalAddon);

        try
        {
            RootNode.RestoreNodeVirtualTable();
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        NativeMemory.Copy(originalVirtualTable, modifiedVirtualTable, 0x8 * VIRTUAL_TABLE_ENTRY_COUNT);

        if (InternalAddon->VirtualTable == modifiedVirtualTable)
            InternalAddon->VirtualTable = originalVirtualTable;

        // 不释放 modifiedVirtualTable, 因为 Dalamud 的 OriginalVirtualTable 可能指向它
        // 释放会导致 Dalamud 持有悬空指针, 后续调用时崩溃
        // 此处造成的少量内存泄漏仅插件重载时发生, 可接受
        modifiedVirtualTable = null;

        disposeHandle?.Dispose();
        disposeHandle = null;

        InternalAddon = null;
    }

    private bool isFinalized;

    private bool isSetup;

    /// <summary>
    ///     Handles native events routed to this addon after component event processing.
    /// </summary>
    protected virtual void OnReceiveGlobalEvent
    (
        AtkUnitBase*  addon,
        AtkEventType  eventType,
        int           eventParam,
        AtkEvent*     atkEvent,
        AtkEventData* atkEventData
    ) =>
        originalVirtualTable->ReceiveGlobalEvent(addon, eventType, eventParam, atkEvent, atkEventData);

    /// <summary>
    ///     OnSetup Callback for an addon, this is called to attach and save references to created nodes.
    /// </summary>
    protected virtual void OnSetup
    (
        AtkUnitBase*   addon,
        Span<AtkValue> atkValueSpan
    )
    {
    }

    /// <summary>
    ///     OnShow Callback for an addon, this is called when the window is opened.
    /// </summary>
    /// <remarks>
    ///     Child addons may be shown again without allocating a new instance.
    /// </remarks>
    protected virtual void OnShow
    (
        AtkUnitBase* addon
    )
    {
    }

    /// <summary>
    ///     OnHide Callback for an addon, this is called when the window is opened.
    /// </summary>
    /// <remarks>
    ///     When <see cref="CloseOnHide" /> is true, hiding starts closing and eventually calls <see cref="OnFinalize" />.
    ///     Child addons remain allocated while hidden.
    /// </remarks>
    protected virtual void OnHide
    (
        AtkUnitBase* addon
    )
    {
    }

    /// <summary>
    ///     OnDraw Callback for an addon, this is called every frame the addon is visible.
    /// </summary>
    protected virtual void OnDraw
    (
        AtkUnitBase* addon
    )
    {
    }

    /// <summary>
    ///     OnUpdate Callback for an addon, this is called every frame the addon exists before its opened, and after it's
    ///     closed but not finalized yet.
    /// </summary>
    protected virtual void OnUpdate
    (
        AtkUnitBase* addon
    )
    {
    }

    /// <summary>
    ///     OnFinalize Callback for the addon, this is called immediately before it is deallocated/closed fully.
    /// </summary>
    protected virtual void OnFinalize
    (
        AtkUnitBase* addon
    )
    {
    }

    /// <summary>
    ///     OnRequestedUpdate Callback for the addon, this is only called if you subscribe to string/number array data entries.
    /// </summary>
    protected virtual void OnRequestedUpdate
    (
        AtkUnitBase*      addon,
        NumberArrayData** numberArrayData,
        StringArrayData** stringArrayData
    )
    {
    }

    /// <summary>
    ///     OnRefresh Callback for the addon, the game calls this once on open, and may trigger it under other unknown
    ///     conditions.
    /// </summary>
    protected virtual void OnRefresh
    (
        AtkUnitBase*   addon,
        Span<AtkValue> atkValues
    )
    {
    }

    private void Initialize
    (
        AtkUnitBase* thisPtr
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Initialize");

        originalVirtualTable->Initialize(thisPtr);

        var widgetInfo = IMemorySpace.GetUISpace()->MallocZeroed<AtkUldWidgetInfo>();
        widgetInfo->Id        = 1;
        widgetInfo->NodeCount = 0;
        widgetInfo->NodeList  = null;
        widgetInfo->WidgetAlignment = new AtkWidgetAlignment
        {
            AlignmentType = AlignmentType.Center,
            X             = 50.0f,
            Y             = 50.0f
        };

        thisPtr->UldManager.InitializeResourceRendererManager();
        InternalAddon->UldManager.ResourceFlags |= AtkUldManagerResourceFlag.Initialized;

        InternalAddon->UldManager.Objects       =  (AtkUldObjectInfo*)widgetInfo;
        InternalAddon->UldManager.ObjectCount   =  1;
        InternalAddon->UldManager.ResourceFlags |= AtkUldManagerResourceFlag.ArraysAllocated;

        RootNode.AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(1, 89)
                .AddLabel(1,  101, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(10, 102, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(20, 103, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(30, 104, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(40, 105, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(50, 106, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(60, 107, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(70, 108, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(80, 109, AtkTimelineJumpBehavior.PlayOnce, 0)
                .EndFrameSet()
                .Build()
        );

        InternalAddon->RootNode = RootNode;
        InternalAddon->UldManager.AddNodeToObjectList(RootNode);

        if (!IsOverlayAddon && WindowNode is not null)
        {
            WindowNode.AttachNode(this, NodePosition.AsFirstChild);
            InternalAddon->WindowNode = WindowNode;
            InternalAddon->UldManager.AddNodeToObjectList(WindowNode);
        }

        WindowNode?.WindowHeaderFocusNode.AddNodeFlags(NodeFlags.Focusable);
        InternalAddon->FocusNode = WindowNode is not null ?
                                       WindowNode.WindowHeaderFocusNode :
                                       RootNode;

        InternalAddon->UldManager.UpdateDrawNodeList();
        InternalAddon->UldManager.LoadedState = AtkLoadState.Loaded;

        InternalAddon->LoadState              = AtkUnitBaseLoadState.LoadingUldResource;
        InternalAddon->WasLoadUldByNameCalled = true;
        InternalAddon->UpdateCollisionNodeList(false);

        // Now that we have constructed this instance, track it for auto-dispose
        CreatedAddons.Add(this);
    }

    private void Setup
    (
        AtkUnitBase* addon,
        uint         valueCount,
        AtkValue*    values
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Setup");

        if (!IsOverlayAddon)
            SetInitialState();
        else
        {
            ref var screenSize = ref AtkStage.Instance()->ScreenSize;

            addon->SetScale(1.0f / AtkUnitBase.GetGlobalUIScale(), true);
            addon->SetSize((ushort)screenSize.Width, (ushort)screenSize.Height);
            addon->SetPosition(0, 0);
        }

        originalVirtualTable->OnSetup(addon, valueCount, values);

        try
        {
            OnSetup(addon, new Span<AtkValue>(values, (int)valueCount));
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        // Initialize all TextId fields that were attached in OnSetup
        addon->UldManager.SetupTextRecursive();

        isSetup = true;
    }

    private void Show
    (
        AtkUnitBase* addon,
        bool         silenceOpenSoundEffect,
        uint         unsetShowHideFlags
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Show");

        try
        {
            OnShow(addon);
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        originalVirtualTable->Show(addon, silenceOpenSoundEffect, unsetShowHideFlags);
    }

    private void Update
    (
        AtkUnitBase* addon,
        float        delta
    )
    {
        IPluginLog.Get().Excessive($"[{InternalName}] Update");

        try
        {
            if (disposeState is AddonDisposeState.Alive)
                OnUpdate(addon);
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        originalVirtualTable->Update(addon, delta);
    }

    private void Draw
    (
        AtkUnitBase* addon
    )
    {
        IPluginLog.Get().Excessive($"[{InternalName}] Draw");

        try
        {
            OnDraw(addon);
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        originalVirtualTable->Draw(addon);
    }

    private void Hide
    (
        AtkUnitBase* addon,
        bool         unkBool,
        bool         callHideCallback,
        uint         setShowHideFlags
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Hide");

        try
        {
            OnHide(addon);
            if (UsesWindowConfiguration)
                SaveAddonConfig();
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        originalVirtualTable->Hide(addon, unkBool, callHideCallback, setShowHideFlags);
        if (CloseOnHide)
            originalVirtualTable->Close(addon, false);
    }

    private void Hide2
    (
        AtkUnitBase* addon
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Hide2");

        originalVirtualTable->Hide2(addon);
    }

    private void Finalizer
    (
        AtkUnitBase* addon
    )
    {
        if (isFinalized) return;
        isFinalized = true;

        if (this is NativeChildAddon childAddon)
            childAddon.Controller.ChildFinalizing(childAddon);

        IPluginLog.Get().Verbose($"[{InternalName}] Finalize");

        try
        {
            OnFinalize(addon);
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        if (RememberClosePosition && InternalAddon is not null)
            LastClosePosition = new Vector2(InternalAddon->X, InternalAddon->Y);

        if (RootNode.ResNode is not null)
        {
            var timeline = RootNode.Timeline;
            RootNode.Timeline          = null;
            RootNode.ResNode->Timeline = null;

            try
            {
                timeline?.Dispose();
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        ClearTimelineManager(addon);

        originalVirtualTable->Finalizer(addon);
        isSetup = false;
    }

    private AtkEventListener* Destructor
    (
        AtkUnitBase* addon,
        byte         flags
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Destructor");

        var result = originalVirtualTable->Dtor(addon, flags);

        if ((flags & 1) == 1)
        {
            // Restore original virtual table so the game won't read a dangling pointer if it calls anything on this addon later.
            addon->VirtualTable = originalVirtualTable;

            InternalAddon = null;
            disposeHandle?.Dispose();
            disposeHandle = null;
            CreatedAddons.Remove(this);

            // Free our custom virtual table, the game doesn't know this exists and won't clear it on its own.
            if (modifiedVirtualTable is not null)
            {
                NativeMemoryHelper.Free(modifiedVirtualTable, 0x8 * VIRTUAL_TABLE_ENTRY_COUNT);
                modifiedVirtualTable = null;
            }

            if (disposeState is AddonDisposeState.Disposing)
                disposeState = AddonDisposeState.Disposed;
        }

        return result;
    }

    private void RequestedUpdate
    (
        AtkUnitBase*      thisPtr,
        NumberArrayData** numberArrayData,
        StringArrayData** stringArrayData
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] RequestedUpdate");

        // Prevent calls to OnRequestedUpdate before Setup is completed. The game will try to call this after Show but before Setup
        if (isSetup)
        {
            try
            {
                OnRequestedUpdate(thisPtr, numberArrayData, stringArrayData);

            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        originalVirtualTable->OnRequestedUpdate(thisPtr, numberArrayData, stringArrayData);
    }

    private bool Refresh
    (
        AtkUnitBase* thisPtr,
        uint         valueCount,
        AtkValue*    values
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] Refresh");

        try
        {
            OnRefresh(thisPtr, new Span<AtkValue>(values, (int)valueCount));
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }

        return originalVirtualTable->OnRefresh(thisPtr, valueCount, values);
    }

    private void ScreenSizeChange
    (
        AtkUnitBase* thisPtr,
        int          width,
        int          height
    )
    {
        IPluginLog.Get().Verbose($"[{InternalName}] ScreenSizeChange");

        originalVirtualTable->OnScreenSizeChange(thisPtr, width, height);

        if (IsOverlayAddon || IgnoreGlobalScale)
            thisPtr->SetScale(1.0f / AtkUnitBase.GetGlobalUIScale(), true);
    }

    #region 常量

    private const int VIRTUAL_TABLE_ENTRY_COUNT = 200;

    #endregion
}
