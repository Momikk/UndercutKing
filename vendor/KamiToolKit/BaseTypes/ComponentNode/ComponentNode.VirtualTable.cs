using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.BaseTypes.ComponentNode;

public abstract unsafe partial class ComponentNode
{
    private const int                                     VirtualTableEntryCount = 100;
    private       AtkComponentBase.Delegates.Deinitialize deinitializeFunction   = null!;

    private AtkComponentBase.Delegates.Dtor                        destructorFunction          = null!;
    private AtkComponentBase.Delegates.Draw                        drawFunction                = null!;
    private AtkComponentBase.Delegates.GetAtkResNode               getAtkResNodeFunction       = null!;
    private AtkComponentBase.Delegates.GetFocusNode                getFocusNodeFunction        = null!;
    private AtkComponentBase.Delegates.InitializeFromComponentData initializeFromComponentData = null!;
    private AtkComponentBase.Delegates.Initialize                  initializeFunction          = null!;

    private AtkComponentBase.AtkComponentBaseVirtualTable* modifiedVirtualTable;
    private AtkComponentBase.AtkComponentBaseVirtualTable* originalVirtualTable;
    private AtkComponentBase.Delegates.PlaySoundEffect     playSoundEffectFunction    = null!;
    private AtkComponentBase.Delegates.ReceiveEvent        receiveEventFunction       = null!;
    private AtkComponentBase.Delegates.ReceiveGlobalEvent  receiveGlobalEventFunction = null!;
    private AtkComponentBase.Delegates.SetEnabledState     setEnabledStateFunction    = null!;
    private AtkComponentBase.Delegates.Setup               setupFunction              = null!;
    private AtkComponentBase.Delegates.Update              updateFunction             = null!;

    /// <summary>
    ///     Replaces components original virtual table with a fully managed custom virtual table.
    /// </summary>
    protected void RegisterVirtualTable()
    {
        originalVirtualTable = ComponentBase->VirtualTable;

        modifiedVirtualTable = (AtkComponentBase.AtkComponentBaseVirtualTable*)NativeMemoryHelper.Malloc(0x8 * VirtualTableEntryCount);
        NativeMemory.Copy(ComponentBase->VirtualTable, modifiedVirtualTable, 0x8 * VirtualTableEntryCount);
        ComponentBase->VirtualTable = modifiedVirtualTable;

        destructorFunction          = Destructor;
        receiveGlobalEventFunction  = OnReceiveGlobalEvent;
        receiveEventFunction        = OnReceiveEvent;
        initializeFunction          = OnInitialize;
        deinitializeFunction        = OnDeinitialize;
        updateFunction              = OnUpdate;
        drawFunction                = OnDraw;
        setupFunction               = OnSetup;
        setEnabledStateFunction     = OnSetEnabledState;
        playSoundEffectFunction     = OnPlaySoundEffect;
        getAtkResNodeFunction       = OnGetAtkResNode;
        getFocusNodeFunction        = OnGetFocusNode;
        initializeFromComponentData = OnInitializeFromComponentData;

        modifiedVirtualTable->Dtor = (delegate* unmanaged<AtkComponentBase*, byte, AtkEventListener*>)Marshal.GetFunctionPointerForDelegate(destructorFunction);
        modifiedVirtualTable->ReceiveGlobalEvent =
            (delegate* unmanaged<AtkComponentBase*, AtkEventType, int, AtkEvent*, AtkEventData*, void>)Marshal.GetFunctionPointerForDelegate
                (receiveGlobalEventFunction);
        modifiedVirtualTable->ReceiveEvent =
            (delegate* unmanaged<AtkComponentBase*, AtkEventType, int, AtkEvent*, AtkEventData*, void>)Marshal.GetFunctionPointerForDelegate(receiveEventFunction);
        modifiedVirtualTable->Initialize      = (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(initializeFunction);
        modifiedVirtualTable->Deinitialize    = (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(deinitializeFunction);
        modifiedVirtualTable->Update          = (delegate* unmanaged<AtkComponentBase*, float, void>)Marshal.GetFunctionPointerForDelegate(updateFunction);
        modifiedVirtualTable->Draw            = (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(drawFunction);
        modifiedVirtualTable->Setup           = (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(setupFunction);
        modifiedVirtualTable->SetEnabledState = (delegate* unmanaged<AtkComponentBase*, bool, void>)Marshal.GetFunctionPointerForDelegate(setEnabledStateFunction);
        modifiedVirtualTable->PlaySoundEffect = (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(playSoundEffectFunction);
        modifiedVirtualTable->GetAtkResNode   = (delegate* unmanaged<AtkComponentBase*, AtkResNode*>)Marshal.GetFunctionPointerForDelegate(getAtkResNodeFunction);
        modifiedVirtualTable->GetFocusNode    = (delegate* unmanaged<AtkComponentBase*, AtkResNode*>)Marshal.GetFunctionPointerForDelegate(getFocusNodeFunction);
        modifiedVirtualTable->InitializeFromComponentData = (delegate* unmanaged<AtkComponentBase*, void*, void>)Marshal.GetFunctionPointerForDelegate
            (initializeFromComponentData);
    }

    internal void RestoreComponentVirtualTable()
    {
        if (modifiedVirtualTable is null) return;

        if (ResNode is not null)
        {
            try
            {
                if (ComponentBase is not null && ComponentBase->VirtualTable == modifiedVirtualTable)
                    ComponentBase->VirtualTable = originalVirtualTable;
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
        }

        NativeMemoryHelper.Free(modifiedVirtualTable, 0x8 * VirtualTableEntryCount);
        modifiedVirtualTable = null;
    }

    internal override void RestoreNodeVirtualTable()
    {
        base.RestoreNodeVirtualTable();
        RestoreComponentVirtualTable();
    }

    /// <summary>
    ///     Global event callback for events that the game wired up to this component.
    /// </summary>
    protected virtual void OnReceiveGlobalEvent
    (
        AtkComponentBase* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    ) =>
        originalVirtualTable->ReceiveGlobalEvent(thisPtr, eventType, eventParam, atkEvent, atkEventData);

    /// <summary>
    ///     Event callback for events that the game wired up to this component.
    /// </summary>
    protected virtual void OnReceiveEvent
    (
        AtkComponentBase* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    ) =>
        originalVirtualTable->ReceiveEvent(thisPtr, eventType, eventParam, atkEvent, atkEventData);

    /// <summary>
    ///     Initialize callback for this component.
    /// </summary>
    protected virtual void OnInitialize
    (
        AtkComponentBase* thisPtr
    ) =>
        originalVirtualTable->Initialize(thisPtr);

    /// <summary>
    ///     Unloading callback for this component.
    /// </summary>
    protected virtual void OnDeinitialize
    (
        AtkComponentBase* thisPtr
    ) =>
        originalVirtualTable->Deinitialize(thisPtr);

    /// <summary>
    ///     Per-frame update callback for this component.
    /// </summary>
    protected virtual void OnUpdate
    (
        AtkComponentBase* thisPtr,
        float             delta
    ) =>
        originalVirtualTable->Update(thisPtr, delta);

    /// <summary>
    ///     Draw callback for this component.
    /// </summary>
    protected virtual void OnDraw
    (
        AtkComponentBase* thisPtr
    ) =>
        originalVirtualTable->Draw(thisPtr);

    /// <summary>
    ///     Setup callback for this component.
    /// </summary>
    protected virtual void OnSetup
    (
        AtkComponentBase* thisPtr
    ) =>
        originalVirtualTable->Setup(thisPtr);

    /// <summary>
    ///     Enable state changed callback for this component.
    /// </summary>
    protected virtual void OnSetEnabledState
    (
        AtkComponentBase* thisPtr,
        bool              enabled
    ) =>
        originalVirtualTable->SetEnabledState(thisPtr, enabled);

    /// <summary>
    ///     Play sound effect callback for this component.
    /// </summary>
    protected virtual void OnPlaySoundEffect
    (
        AtkComponentBase* thisPtr
    ) =>
        originalVirtualTable->PlaySoundEffect(thisPtr);

    /// <summary>
    ///     GetAtkResNode callback for this component.
    /// </summary>
    protected virtual AtkResNode* OnGetAtkResNode
    (
        AtkComponentBase* thisPtr
    )
    {
        var result = originalVirtualTable->GetAtkResNode(thisPtr);

        return result;
    }

    /// <summary>
    ///     GetFocusNode callback for this component.
    /// </summary>
    /// <remarks>
    ///     Overriden to return <see cref="FocusNode" />.
    /// </remarks>
    protected virtual AtkResNode* OnGetFocusNode
    (
        AtkComponentBase* thisPtr
    ) =>
        FocusNode;

    /// <summary>
    ///     Initialization from data callback for this component.
    /// </summary>
    /// <param name="thisPtr"></param>
    /// <param name="data"></param>
    protected virtual void OnInitializeFromComponentData
    (
        AtkComponentBase* thisPtr,
        void*             data
    ) =>
        originalVirtualTable->InitializeFromComponentData(thisPtr, data);

    private AtkEventListener* Destructor
    (
        AtkComponentBase* thisPtr,
        byte              freeFlags
    )
    {
        var result = originalVirtualTable->Dtor(thisPtr, freeFlags);

        if ((freeFlags & 1) != 0 && modifiedVirtualTable is not null)
        {
            // Free our custom virtual table, the game doesn't know this exists and won't clear it on its own.
            NativeMemoryHelper.Free(modifiedVirtualTable, 0x8 * VirtualTableEntryCount);
            modifiedVirtualTable = null;
        }

        return result;
    }
}
