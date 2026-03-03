using HarmonyLib;
using RimWorld;
using Verse;

namespace IngredientThreshold.Patches
{
    [HarmonyPatch(typeof(Bill_Production), "ShouldDoNow")]
    public static class Patch_Bill_ShouldDoNow
    {
        public static bool Prefix(Bill_Production __instance, ref bool __result)
        {
            if (__instance.repeatMode != ThresholdRepeatModeDef.IngredientThreshold)
                return true;

            if (__instance.suspended)
            {
                __result = false;
                return false;
            }

            if (!Patch_Bill_ExposeData.extraData.TryGetValue(__instance, out var data)
                || data.ingredient == null)
            {
                __result = false;
                return false;
            }

            var map = (__instance.billStack?.billGiver as Thing)?.Map;
            if (map == null)
            {
                __result = false;
                return false;
            }

            __result = map.resourceCounter.GetCount(data.ingredient) > data.threshold;
            return false;
        }
    }
}
