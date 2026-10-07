using HarmonyLib;

namespace MiniRealisticAirways;

// The stock UpdateWaypoint commits HARWCurWP only after the real handoff
// condition succeeds. Prediction uses a separate HARWCurWPPreCalc property.
// Each assignment represents one passage, even when returning to the same WP.
[HarmonyPatch(typeof(Aircraft), "HARWCurWP", MethodType.Setter)]
internal static class PatchWaypointArrival
{
	private static void Postfix(Aircraft __instance, PlaceableWaypoint __0)
	{
		if (!(__0 is BaseWaypointAutoHeading) || __instance == null
			|| __instance.state != Aircraft.State.HeadingAfterReachingWaypoint
			|| !AircraftState.GetAircraftState(__instance, out var state)) return;
		var altitude = state.aircraftAltitude_;
		if (altitude == null) return;

		if (__0 is WaypointAutoLanding)
		{
			// Auto-landing remains an absolute Low clearance, never a loop of
			// relative descents. Safety can veto it without queuing stale orders.
			if (__0.GetFieldValue<Runway>("_targetRunway") == null) return;
			altitude.SetTargetAltitude(AltitudeLevel.Low);
			return;
		}

		var waypointAltitude = __0.GetComponent<WaypointAltitude>();
		if (waypointAltitude != null) altitude.SetTargetAltitude(waypointAltitude.altitude_);
		var waypointSpeed = __0.GetComponent<WaypointSpeed>();
		if (state.aircraftSpeed_ != null && waypointSpeed != null)
			state.aircraftSpeed_.SetTargetSpeed(waypointSpeed.speed_);
	}
}
