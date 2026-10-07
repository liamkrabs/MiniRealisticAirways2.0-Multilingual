using System;
using HarmonyLib;

namespace MiniRealisticAirways;

[HarmonyPatch(typeof(Aircraft), "UpdateHeading", new Type[] { })]
internal class PatchUpdateHeading
{
	private static void Prefix(Aircraft __instance, PlaceableWaypoint ____HARWCurWP, out TurnSpeedScope __state)
	{
		__state = TurnSpeedScope.Enter(__instance);
		// Preserve the existing auto-landing speed protection. Altitude is now
		// issued once by PatchWaypointArrival, not every UpdateHeading frame.
		if (__instance == null || __instance.state != Aircraft.State.HeadingAfterReachingWaypoint
			|| !(____HARWCurWP is WaypointAutoLanding)
			|| ____HARWCurWP.GetFieldValue<Runway>("_targetRunway") == null
			|| !AircraftState.GetAircraftStates(__instance, out var _, out var speed, out var type)) return;
		for (int i = 0; speed != null && type != null && i < Plugin.MAX_WHILE_LOOP_ITER && !speed.CanLand(type.weight_); i++)
		{
			float previous = __instance.targetSpeed;
			speed.AircraftSlowDown();
			if (__instance.targetSpeed == previous) break;
		}
	}

	private static void Postfix(ref TurnSpeedScope __state) => __state.Dispose();

	private static Exception Finalizer(ref TurnSpeedScope __state, Exception __exception)
	{
		__state.Dispose();
		return __exception;
	}
}
