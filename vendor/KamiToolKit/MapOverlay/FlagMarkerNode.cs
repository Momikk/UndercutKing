using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace KamiToolKit.MapOverlay;

/// <summary>
///     Specialized implementation of a <see cref="MapMarkerNode" /> for use in <see cref="MapOverlayController" />
///     to render a flag marker node over top all the other custom marker nodes so flags can still be seen when using the
///     overlay.
/// </summary>
public unsafe class FlagMarkerNode : MapMarkerNode
{
    /// <summary>
    ///     Constructs a instance of a <see cref="FlagMarkerNode" />
    /// </summary>
    public FlagMarkerNode()
    {
        IconId      = 60561;
        AllowAnyMap = true;
        Size        = new Vector2(32.0f, 32.0f);
    }

    /// <inheritdoc />
    protected override void OnUpdate()
    {
        var agentMap = AgentMap.Instance();

        ref var flagMarker = ref agentMap->FlagMapMarkers[0];

        if (IconId != flagMarker.MapMarker.IconId)
            IconId = flagMarker.MapMarker.IconId;

        // Follow the currently selected map so Update() can convert the world position
        MapId = agentMap->SelectedMapId;

        // The flag position is already in world coordinates
        Position  = new Vector2(flagMarker.XFloat, flagMarker.YFloat);
        IsVisible = agentMap->FlagMarkerCount is not 0 && flagMarker.TerritoryId == agentMap->SelectedTerritoryId;

        base.OnUpdate();
    }
}
