using UnityEngine;
using UnityEngine.Rendering;

namespace MiniRealisticAirways;

// Treat the navigation symbol and every child HUD as one foreground object.
// Separate Text/1 child groups otherwise tie newly spawned destination labels.
internal sealed class WaypointVisualPriority : MonoBehaviour
{
    internal const int NavigationOrder = 32767;
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
        if (group.sortingLayerName != "Text") group.sortingLayerName = "Text";
        if (group.sortingOrder != NavigationOrder) group.sortingOrder = NavigationOrder;
        group.sortAtRoot = true;
    }

    // LateUpdate also runs while paused and is independent of text-cache hits.
    private void LateUpdate() => EnsurePriority();
}
