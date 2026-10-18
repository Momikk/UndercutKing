using System.Numerics;
using Dalamud.Game.Addon.Events;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Nodes;
using MapMarkerInfo = KamiToolKit.Classes.MapMarkerInfo;

namespace KamiToolKit.MapOverlay;

/// <summary>
///     Controller for <see cref="MapMarkerNode" />'s that are rendered over top of the games native map.
/// </summary>
public unsafe class MapOverlayController : IDisposable
{
    private readonly AddonController<AddonAreaMap> mapController;

    private readonly List<MapMarkerNode> markerNodes = [];

    private readonly List<MapMarkerInfo> queuedMarkers = [];
    private readonly List<MapMarkerNode> queuedNodes   = [];
    private          ResNode?            clippingContainerNode;
    private          ResNode?            flagContainerNode;

    private MapMarkerNode? flagNode;
    private ResNode?       overlayNode;

    private bool                   showingInteractCursor;
    private ViewportEventListener? viewportEventListener;

    /// <summary>
    ///     Constructs a <see cref="MapOverlayController" /> instance.
    /// </summary>
    public MapOverlayController() =>
        mapController = new AddonController<AddonAreaMap>
        {
            AddonName   = "AreaMap",
            OnSetup     = OnAttach,
            OnPreUpdate = OnUpdate,
            OnFinalize  = OnDetach
        };

    public Action<uint, Vector2>? OnMapClick { get; set; }

    /// <summary>
    ///     Gets or sets whether the overlay is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether markers are hidden while the control key is held down.
    /// </summary>
    public bool HideMarkersOnControlKey { get; set; } = false;

    /// <summary>
    ///     Disposes controller and all nodes in this controller.
    /// </summary>
    /// <remarks>
    ///     Must be called from the main thread.
    /// </remarks>
    public void Dispose()
    {
        Disable();

        viewportEventListener?.Dispose();
        viewportEventListener = null;

        mapController.Dispose();

        RemoveAllMarkers();

        overlayNode?.Dispose();
        overlayNode = null;

        clippingContainerNode?.Dispose();
        clippingContainerNode = null;
    }

    /// <summary>
    ///     Enables the map controller.
    /// </summary>
    /// <remarks>
    ///     Must be called from the main thread.
    /// </remarks>
    public void Enable() =>
        mapController.Enable();

    /// <summary>
    ///     Disables the map controller.
    /// </summary>
    /// <remarks>
    ///     Must be called from the main thread.
    /// </remarks>
    public void Disable() =>
        mapController.Disable();

    /// <summary>
    ///     Adds a single marker to the map with the provided info.
    /// </summary>
    public void AddMarker
    (
        MapMarkerInfo markerInfo
    )
        => queuedMarkers.Add(markerInfo);

    /// <summary>
    ///     Adds a single <see cref="MapMarkerNode" /> to the map.
    /// </summary>
    /// <remarks>
    ///     The overlay then takes ownership of this node. Manually disposing this node will break things.
    /// </remarks>
    public void AddMarker
    (
        MapMarkerNode marker
    )
        => queuedNodes.Add(marker);

    /// <summary>
    ///     Removes and disposes the specified marker from the overlay.
    /// </summary>
    public void RemoveMarker
    (
        MapMarkerNode marker
    )
    {
        if (queuedNodes.Remove(marker))
            marker.Dispose();

        if (markerNodes.Remove(marker))
            marker.Dispose();
    }

    /// <summary>
    ///     Removes and dispose all map markers for this overlay.
    /// </summary>
    public void RemoveAllMarkers()
    {
        foreach (var node in markerNodes)
            node.Dispose();
        markerNodes.Clear();

        foreach (var node in queuedNodes)
            node.Dispose();
        queuedNodes.Clear();

        queuedMarkers.Clear();
    }

    private void OnAttach
    (
        AddonAreaMap* addon
    )
    {
        var mapComponentNode = addon->GetNodeById(53);
        if (mapComponentNode is null) return;

        clippingContainerNode = new ResNode
        {
            NodeFlags = NodeFlags.Clip | NodeFlags.Visible
        };
        clippingContainerNode.AttachNode(mapComponentNode, NodePosition.AfterTarget);

        viewportEventListener = new ViewportEventListener(OnViewportEvent);
        viewportEventListener.AddEvent(AtkEventType.MouseMove, clippingContainerNode);
        viewportEventListener.AddEvent(AtkEventType.MouseDown, clippingContainerNode);

        overlayNode = new ResNode();
        overlayNode.AttachNode(clippingContainerNode);

        flagContainerNode = new ResNode();
        flagContainerNode.AttachNode(clippingContainerNode);

        flagNode = new FlagMarkerNode();
        flagNode.AttachNode(flagContainerNode);
    }

    private void OnUpdate
    (
        AddonAreaMap* addon
    )
    {
        if (clippingContainerNode is null) return;
        if (overlayNode is null) return;

        var agentMap = AgentMap.Instance();

        if (HideMarkersOnControlKey && agentMap->IsControlKeyPressed && AnyMarkerTooltipShowing())
            AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);

        ProcessQueues();

        ref var areaMap = ref addon->AreaMap;

        var mapComponent = areaMap.ComponentMap;
        if (mapComponent is null) return;

        var controlKeyPressed = agentMap->IsControlKeyPressed;

        clippingContainerNode.IsVisible = IsVisible;

        clippingContainerNode.Size     = mapComponent->OwnerNode->AtkResNode.Size;
        clippingContainerNode.Position = mapComponent->OwnerNode->AtkResNode.Position;

        var mapComponentNode = mapComponent->OwnerNode->AtkResNode;
        var center           = (mapComponentNode.Size / 2.0f) + new Vector2(18.0f, 46.0f);

        overlayNode.Scale = new Vector2(areaMap.MapScale, areaMap.MapScale);
        overlayNode.Size  = new Vector2(2048.0f,          2048.0f);

        // Start with current position
        var offset = new Vector2(areaMap.MapOffsetX, areaMap.MapOffsetY);

        // Add map-specific offset using the selected map
        offset += new Vector2(agentMap->SelectedOffsetX, agentMap->SelectedOffsetY);

        // Set object position relative to center of node
        offset += overlayNode.Size / 2.0f;

        // Scale to current Zoom Level
        offset *= mapComponent->MapScale;

        overlayNode.Position = center - offset - clippingContainerNode.Position;

        foreach (var marker in markerNodes)
        {
            marker.Update();
            marker.Scale = Vector2.One / new Vector2(areaMap.MarkerPositionScaling, areaMap.MarkerPositionScaling);

            // Hide markers while the control key is held down, without touching the
            // markers own IsVisible value so it recovers automatically
            if (controlKeyPressed && HideMarkersOnControlKey)
                marker.ResNode->Visible = false;

            marker.RefreshInteractivity();
            marker.UpdateTooltipFollowMouse();
        }

        UpdateFlagNode(areaMap);
    }

    private void OnDetach
    (
        AddonAreaMap* addon
    )
    {
        viewportEventListener?.Dispose();
        viewportEventListener = null;

        foreach (var marker in markerNodes)
        {
            marker.DetachNode();
            queuedNodes.Add(marker);
        }

        markerNodes.Clear();

        clippingContainerNode?.Dispose();
        clippingContainerNode = null;

        overlayNode?.Dispose();
        overlayNode = null;
    }

    private void ProcessQueues()
    {
        foreach (var markerInfo in queuedMarkers)
        {
            var newMarkerNode = new MapMarkerNode
            {
                IconId      = markerInfo.IconId,
                MapId       = markerInfo.MapId,
                Texture     = markerInfo.Texture,
                TexturePath = markerInfo.TexturePath,
                Size        = markerInfo.Size ?? new Vector2(16.0f, 16.0f),
                Origin      = (markerInfo.Size ?? new Vector2(16.0f, 16.0f)) / 2.0f,
                Position    = markerInfo.Position ?? new Vector2(1024.0f, 1024.0f),
                TextTooltip = markerInfo.Tooltip  ?? string.Empty,
                AllowAnyMap = markerInfo.AllowAnyMap
            };

            markerNodes.Add(newMarkerNode);
            newMarkerNode.AttachNode(overlayNode);
        }

        queuedMarkers.Clear();

        foreach (var markerNode in queuedNodes)
        {
            markerNodes.Add(markerNode);
            markerNode.AttachNode(overlayNode);
        }

        queuedNodes.Clear();
    }

    private void UpdateFlagNode
    (
        Atk2DAreaMap areaMap
    )
    {
        if (overlayNode is null) return;

        if (flagContainerNode is not null && flagNode is not null)
        {
            flagContainerNode.Size     = overlayNode.Size;
            flagContainerNode.Scale    = overlayNode.Scale;
            flagContainerNode.Position = overlayNode.Position;

            flagNode.Update();
            flagNode.Scale = Vector2.One / new Vector2(areaMap.MarkerPositionScaling, areaMap.MarkerPositionScaling);
        }
    }

    private void OnViewportEvent
    (
        AtkEventListener* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    )
    {
        switch (eventType)
        {
            case AtkEventType.MouseMove:
                ProcessMouseMove(atkEventData);
                break;

            case AtkEventType.MouseDown when !AgentMap.Instance()->IsControlKeyPressed || !HideMarkersOnControlKey:
                ProcessMouseClick(atkEventData);
                break;
        }
    }

    private bool AnyMarkerTooltipShowing()
    {
        foreach (var marker in markerNodes)
        {
            if (marker.TooltipShowing)
                return true;
        }

        return false;
    }

    private void ProcessMouseMove
    (
        AtkEventData* atkEventData
    )
    {
        if (clippingContainerNode is null) return;

        var mapAddon = RaptureAtkUnitManager.Instance()->GetAddonByName("AreaMap");
        if (mapAddon is null) return;

        if (RaptureAtkModule.Instance()->AtkCollisionManager.IntersectingAddon != mapAddon) return;

        if (mapAddon->NumBlockingAddons != 0) return;

        var anyInteractions = false;

        if (!AgentMap.Instance()->IsControlKeyPressed || !HideMarkersOnControlKey)
        {
            foreach (var node in markerNodes)
            {
                if (!node.IsActuallyVisible || !node.CheckCollision(atkEventData) || !clippingContainerNode.CheckCollision(atkEventData))
                    continue;

                if (node.OnClick is not null || node.OnRightClick is not null)
                {
                    IAddonEventManager.Get().SetCursor(AddonCursorType.Clickable);
                    showingInteractCursor = true;
                    anyInteractions       = true;
                }
            }
        }

        if (!anyInteractions && showingInteractCursor)
        {
            IAddonEventManager.Get().ResetCursor();
            showingInteractCursor = false;
        }
    }

    private void ProcessMouseClick
    (
        AtkEventData* atkEventData
    )
    {
        var isRightClick = atkEventData->MouseData.ButtonId is 1;
        if (!isRightClick && atkEventData->MouseData.ButtonId is not 0) return;

        var mapAddon = RaptureAtkUnitManager.Instance()->GetAddonByName("AreaMap");
        if (mapAddon is null || mapAddon->NumBlockingAddons != 0) return;

        // Marker clicks are handled by the marker nodes themselves, but any click
        // over an interactive marker must not fall through to the empty map click.
        if (!isRightClick)
        {
            foreach (var node in markerNodes)
            {
                if (node.IsActuallyVisible && node.CheckCollision(atkEventData))
                    return;
            }
        }

        if (isRightClick) return;

        if (TryGetMapPosition(atkEventData, out var mapId, out var mapPosition))
            OnMapClick?.Invoke(mapId, mapPosition);
    }

    private bool TryGetMapPosition
    (
        AtkEventData* atkEventData,
        out uint      mapId,
        out Vector2   mapPosition
    )
    {
        mapId       = AgentMap.Instance()->SelectedMapId;
        mapPosition = default;

        if (overlayNode is null) return false;

        var node = overlayNode.ResNode;
        if (node is null) return false;
        var cumulativeScale = Vector2.One;
        for (var currentNode = node; currentNode is not null; currentNode = currentNode->ParentNode)
            cumulativeScale *= new Vector2(currentNode->ScaleX, currentNode->ScaleY);

        if (cumulativeScale.X is 0.0f || cumulativeScale.Y is 0.0f) return false;

        var mousePosition = new Vector2(atkEventData->MouseData.PosX, atkEventData->MouseData.PosY);
        var localPosition = (mousePosition - new Vector2(node->ScreenX, node->ScreenY)) / cumulativeScale;
        var agentMap      = AgentMap.Instance();
        var mapScale      = agentMap->SelectedMapSizeFactorFloat;
        if (mapScale is 0.0f) return false;

        var selectedOffset = new Vector2(agentMap->SelectedOffsetX, agentMap->SelectedOffsetY);
        // Invert the marker mapping: node = (world * scale) + offset * (scale - 1) + 1024
        mapPosition = ((localPosition - new Vector2(1024.0f) + selectedOffset) / mapScale) - selectedOffset;
        return true;
    }
}
