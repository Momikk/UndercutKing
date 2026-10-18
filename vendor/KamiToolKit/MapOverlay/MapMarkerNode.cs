using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Action = System.Action;

namespace KamiToolKit.MapOverlay;

/// <summary>
///     Inheritable node intended for use with <see cref="MapOverlayController" />.
/// </summary>
public unsafe class MapMarkerNode : ResNode
{
    private const NodeFlags       InteractiveNodeFlags = NodeFlags.HasCollision | NodeFlags.RespondToMouse | NodeFlags.EmitsEvents;
    private       IconImageNode?  iconNode;
    private       ImGuiImageNode? imGuiImageNode;
    private       bool            interactionRegistered;

    private Action?          onClick;
    private Action?          onRightClick;
    private ReadOnlySeString textTooltip = string.Empty;

    /// <summary>
    ///     Gets or sets the action to be called when this marker is clicked on.
    /// </summary>
    public Action? OnClick
    {
        get => onClick;
        set
        {
            onClick = value;
            RefreshInteractivity();
        }
    }

    public Action? OnRightClick
    {
        get => onRightClick;
        set
        {
            onRightClick = value;
            RefreshInteractivity();
        }
    }

    /// <summary>
    ///     Gets whether this node is actually being shown.
    /// </summary>
    public bool IsActuallyVisible
        => ResNode is not null && ResNode->IsActuallyVisible;

    /// <summary>
    ///     Gets whether a tooltip for this marker is currently being shown.
    /// </summary>
    public bool TooltipShowing { get; private set; }

    /// <summary>
    ///     Gets or sets the markers visibility.
    /// </summary>
    public override bool IsVisible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the markers size in pixels.
    /// </summary>
    public new Vector2 Size { get; set; }

    /// <summary>
    ///     Gets or sets the markers scale. Default is 1.0f.
    /// </summary>
    public float MarkerScale { get; set; } = 1.0f;

    /// <summary>
    ///     Gets or sets the markers position on the map.
    /// </summary>
    /// <remarks>
    ///     Expects the position in world coordinates on the XZ plane.
    /// </remarks>
    public new Vector2 Position { get; set; }

    /// <summary>
    ///     Gets or sets the tooltip shown when hovering over this marker.
    /// </summary>
    public new ReadOnlySeString TextTooltip
    {
        get => textTooltip;
        set
        {
            textTooltip = value;
            RefreshInteractivity();
        }
    }

    /// <summary>
    ///     Gets or sets the mapId this marker is allowed to be in.
    ///     Use <see cref="AllowAnyMap" /> to allow on any map.
    /// </summary>
    public uint MapId { get; set; }

    /// <summary>
    ///     Gets or sets whether this marker is allowed to be shown when viewing any map.
    /// </summary>
    public bool AllowAnyMap { get; set; }

    /// <summary>
    ///     Gets or sets the iconId to be shown with this marker.
    /// </summary>
    /// <remarks>
    ///     Setting this will unload any <see cref="Texture" /> or <see cref="TexturePath" /> that is set.
    /// </remarks>
    public uint? IconId
    {
        get;
        set
        {
            if (value is null) return;
            field = value;

            if (iconNode is not null)
            {
                iconNode.IconId = value.Value;
                return;
            }

            imGuiImageNode?.Dispose();
            imGuiImageNode = null;

            iconNode = new IconImageNode
            {
                IconId     = value.Value,
                FitTexture = true
            };

            iconNode.AttachNode(this);
        }
    } = 0;

    /// <summary>
    ///     Gets or sets the DalamudTextureWrap to be shown with this marker.
    /// </summary>
    /// <remarks>
    ///     Setting this will unload any <see cref="IconId" /> or <see cref="TexturePath" /> that is set.
    /// </remarks>
    public IDalamudTextureWrap? Texture
    {
        get;
        set
        {
            if (value is null) return;
            field = value;

            iconNode?.Dispose();
            iconNode = null;

            imGuiImageNode?.Dispose();
            imGuiImageNode = null;

            imGuiImageNode = new ImGuiImageNode
            {
                FitTexture = true
            };
            imGuiImageNode.LoadTexture(value);
            imGuiImageNode.AttachNode(this);
        }
    } = null;

    /// <summary>
    ///     Gets or sets the texture path to load and to be shown with this marker.
    /// </summary>
    /// <remarks>
    ///     Setting this will unload any <see cref="IconId" /> or <see cref="Texture" /> that is set.
    /// </remarks>
    public string? TexturePath
    {
        get;
        set
        {
            if (value is null) return;
            field = value;

            iconNode?.Dispose();
            iconNode = null;

            imGuiImageNode?.Dispose();
            imGuiImageNode = null;

            imGuiImageNode = new ImGuiImageNode
            {
                TexturePath = value,
                FitTexture  = true
            };
            imGuiImageNode.AttachNode(this);
        }
    } = null;

    /// <summary>
    ///     Updates the nodes size position and scale according to the params of the specific map being shown.
    ///     Triggers <see cref="OnUpdate" />.
    /// </summary>
    public void Update()
    {
        OnUpdate();

        var centerOffset = new Vector2(1024.0f, 1024.0f);

        if (!IDataManager.Get().GetExcelSheet<Map>().TryGetRow(MapId, out var mapRow))
        {
            IsVisible = false;
            return;
        }

        // Convert world coordinates to node coordinates: the overlay node space is offset
        // from texture space by the map offset, e.g. node = texture - Offset
        var mapScale       = mapRow.SizeFactor                           / 100.0f;
        var mapOffset      = new Vector2(mapRow.OffsetX, mapRow.OffsetY) * (mapScale - 1);
        var markerPosition = (Position * mapScale) + mapOffset;

        base.Size   = Size      * MarkerScale;
        base.Origin = base.Size / 2.0f;

        iconNode?.Size   = base.Size;
        iconNode?.Origin = base.Size / 2.0f;

        imGuiImageNode?.Size   = base.Size;
        imGuiImageNode?.Origin = base.Size / 2.0f;

        base.Position  = markerPosition + centerOffset - (base.Size / 2.0f);
        base.IsVisible = IsVisible && (AllowAnyMap || AgentMap.Instance()->SelectedMapId == MapId);
    }

    /// <summary>
    ///     Re-evaluates whether this marker should intercept mouse input, registering or unregistering
    ///     the corresponding node events, and calls an addons collision update if necessary.
    /// </summary>
    /// <remarks>
    ///     Interacting with the map grants this marker priority over any native marker it is near:
    ///     the marker captures mouse over and click input before the native map handlers can react.
    /// </remarks>
    public void RefreshInteractivity()
    {
        if (ResNode is null) return;

        var isInteractive = !textTooltip.IsEmpty || onClick is not null || onRightClick is not null;

        if (isInteractive && !interactionRegistered)
            RegisterInteractionEvents();
        else if (!isInteractive && interactionRegistered)
            UnregisterInteractionEvents();

        // Keep the collision flags in sync so hidden markers never block native input.
        var hasCollisionState   = (ResNode->NodeFlags & InteractiveNodeFlags) == InteractiveNodeFlags;
        var needsCollisionState = isInteractive && ResNode->IsActuallyVisible;

        if (hasCollisionState == needsCollisionState) return;

        if (needsCollisionState)
            AddNodeFlags(InteractiveNodeFlags);
        else
            RemoveNodeFlags(InteractiveNodeFlags);

        if (ParentAddon is not null)
            ParentAddon->UpdateCollisionNodeList(false);
    }

    private void RegisterInteractionEvents()
    {
        AddEvent(AtkEventType.MouseOver, (AtkEventListener.Delegates.ReceiveEvent)OnMouseOverEvent);
        AddEvent(AtkEventType.MouseMove, (AtkEventListener.Delegates.ReceiveEvent)OnMouseMoveEvent);
        AddEvent(AtkEventType.MouseOut,  (AtkEventListener.Delegates.ReceiveEvent)OnMouseOutEvent);
        AddEvent(AtkEventType.MouseDown, (AtkEventListener.Delegates.ReceiveEvent)OnMouseDownEvent);
        interactionRegistered = true;
    }

    private void UnregisterInteractionEvents()
    {
        RemoveEvent(AtkEventType.MouseOver, (AtkEventListener.Delegates.ReceiveEvent)OnMouseOverEvent);
        RemoveEvent(AtkEventType.MouseMove, (AtkEventListener.Delegates.ReceiveEvent)OnMouseMoveEvent);
        RemoveEvent(AtkEventType.MouseOut,  (AtkEventListener.Delegates.ReceiveEvent)OnMouseOutEvent);
        RemoveEvent(AtkEventType.MouseDown, (AtkEventListener.Delegates.ReceiveEvent)OnMouseDownEvent);
        interactionRegistered = false;
    }

    private void OnMouseOverEvent
    (
        AtkEventListener* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    )
    {
        if (ParentAddon is null || textTooltip.IsEmpty) return;

        ShowTooltipFollowingMouse();
        TooltipShowing = true;
        atkEvent->SetEventIsHandled(true);
    }

    private void OnMouseMoveEvent
    (
        AtkEventListener* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    ) =>
        // Consume the mouse move while over this marker so the native map hover
        // detection never runs for the markers underneath.
        atkEvent->SetEventIsHandled(true);

    private void OnMouseOutEvent
    (
        AtkEventListener* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    )
    {
        if (ParentAddon is null) return;

        HideTooltip();
        TooltipShowing = false;
    }

    private void OnMouseDownEvent
    (
        AtkEventListener* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    )
    {
        if (atkEventData->MouseData.ButtonId is 0)
            onClick?.Invoke();
        else if (atkEventData->MouseData.ButtonId is 1)
            onRightClick?.Invoke();

        atkEvent->SetEventIsHandled(true);
    }

    /// <summary>
    ///     Re-shows this markers tooltip anchored to the current mouse position.
    /// </summary>
    /// <remarks>
    ///     Mirrors how the native map markers keep their tooltip glued to the cursor while
    ///     hovering, the map re-shows the tooltip every frame with the follow cursor flag set.
    /// </remarks>
    public void UpdateTooltipFollowMouse()
    {
        if (ParentAddon is null || !TooltipShowing || !IsActuallyVisible) return;

        ShowTooltipFollowingMouse();
    }

    private void ShowTooltipFollowingMouse()
    {
        if (ParentAddon is null || textTooltip.IsEmpty) return;

        using var stringBuilder = new RentedSeStringBuilder();
        using var stringBuffer  = new RentedAtkValues(1);
        stringBuffer[0].SetManagedString(stringBuilder.Builder.Append(textTooltip).GetViewAsSpan());

        var tooltipArgs = new AtkTooltipManager.AtkTooltipArgs
        {
            TextArgs = { AtkArrayType = 0, Text = stringBuffer[0].String }
        };

        // targetNode null and unk7 true both select the "follow the cursor" positioning path,
        // the same one the native map uses for its marker tooltips.
        AtkStage.Instance()->TooltipManager.ShowTooltip(AtkTooltipType.Text, ParentAddon->Id, null, &tooltipArgs, null, true);
    }

    /// <summary>
    ///     Overridable Update Function that is called every frame to update the state of the node.
    /// </summary>
    protected virtual void OnUpdate() { }
}
