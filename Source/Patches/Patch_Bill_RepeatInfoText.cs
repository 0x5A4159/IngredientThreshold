using HarmonyLib;
using RimWorld;

namespace IngredientThreshold.Patches
{
    [HarmonyPatch(typeof(Bill_Production), "get_RepeatInfoText")]
    public static class Patch_Bill_RepeatInfoText
    {
        public static bool Prefix(Bill_Production __instance, ref string __result)
        {
            if (__instance.repeatMode != ThresholdRepeatModeDef.IngredientThreshold)
                return true;

            if (Patch_Bill_ExposeData.extraData.TryGetValue(__instance, out var data)
                && data.ingredient != null)
                __result = $"{data.ingredient.LabelCap} > {data.threshold}";
            else
                __result = "select ingredient";

            return false;
        }
    }
}
