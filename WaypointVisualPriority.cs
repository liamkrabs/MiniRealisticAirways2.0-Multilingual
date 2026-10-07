using UnityEngine;
using UnityEngine.Rendering;

namespace MiniRealisticAirways;

// Keep the complete waypoint above destination labels, below every aircraft.
// Its configured altitude is a command, not a visual depth.
internal sealed class WaypointVisualPriority : MonoBehaviour
{
    internal const int NavigationOrder = FlightVisualOrder.Navigation;
    private SortingGroup group;

    internal static void Attach(PlaceableWaypoint waypoint)
    {
        if (waypoint == null) return;
        WaypointVisualPriority priority = waypoint.GetComponent<WaypointVisualPriority>() ?? waypoint.gameObject.AddComponent<WaypointVisualPriority>();
        priority.EnsurePriority();
    }

    private void EnsurePriority()
    {
        if (group == null) group = GetComponent<SortingGroup>() ?? gameObject.AddComponent<SortingGroup>();
        if (group.sortingLayerName != FlightVisualOrder.LayerName) group.sortingLayerName = FlightVisualOrder.LayerName;
        if (group.sortingOrder != NavigationOrder) group.sortingOrder = NavigationOrder;
        group.sortAtRoot = true;
    }

    // LateUpdate also runs while paused and is independent of text-cache hits.
    private void LateUpdate() => EnsurePriority();
}
