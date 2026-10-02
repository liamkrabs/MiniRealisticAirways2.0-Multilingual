using HarmonyLib;
using TMPro;

namespace MiniRealisticAirways;

[HarmonyPatch(typeof(TMP_SubMeshUI), "UpdateMaterial")]
internal static class TmpSubMeshMaterialLifetimePatch
{
    private static bool Prefix(TMP_SubMeshUI __instance)
    {
        var material = __instance.sharedMaterial;
        if (material == null || !material.HasProperty(ShaderUtilities.ShaderTag_CullMode))
            return true;
        var parent = __instance.textComponent;
        if (parent != null && parent.fontSharedMaterial != null)
            return true;

        // Parent teardown can precede the child OnDisable callback; the stock
        // TMP implementation otherwise reads a destroyed parent material.
        __instance.canvasRenderer.Clear();
        return false;
    }
}
