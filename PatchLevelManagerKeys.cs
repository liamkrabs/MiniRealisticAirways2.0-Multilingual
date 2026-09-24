using HarmonyLib;
using UnityEngine;

namespace MiniRealisticAirways;

// Keep native time controls from handling a naming session's entry/exit twice.
// Input is resolved once per frame, regardless of Unity Update callback order.
[HarmonyPatch(typeof(LevelManager), "OnPauseTimeBtnPressed", new System.Type[] { })]
internal static class PatchLevelManagerPauseKey
{
	private static bool Prefix()
	{
		// Naming performs its own pause/resume; suppress the native toggle for that frame.
		return !WaypointNameInput.ConsumePauseKey();
	}
}

[HarmonyPatch(typeof(LevelManager), "OnNormalTimeBtnPressed", new System.Type[] { })]
internal static class PatchLevelManagerNormalTimeKey
{
	private static bool Prefix()
	{
		return !WaypointNameInput.AnyActive;
	}
}

[HarmonyPatch(typeof(LevelManager), "OnFastTimeBtnPressed", new System.Type[] { })]
internal static class PatchLevelManagerFastTimeKey
{
	private static bool Prefix()
	{
		return !WaypointNameInput.AnyActive;
	}
}

[HarmonyPatch(typeof(LevelManager), "OnEscPressed", new System.Type[] { })]
internal static class PatchLevelManagerEscKey
{
	private static bool Prefix()
	{
		return !WaypointNameInput.AnyActive;
	}
}
