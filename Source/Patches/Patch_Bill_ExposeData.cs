using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace IngredientThreshold.Patches
{
    [HarmonyPatch(typeof(Bill_Production), "ExposeData")]
    public static class Patch_Bill_ExposeData
    {
        internal static readonly ConditionalWeakTable<Bill_Production, BillThresholdData> extraData =
            new ConditionalWeakTable<Bill_Production, BillThresholdData>();

        public static void Postfix(Bill_Production __instance)
        {
            var data = extraData.GetOrCreateValue(__instance);
            Scribe_Defs.Look(ref data.ingredient, "threshold_ingredient");
            Scribe_Values.Look(ref data.threshold, "threshold_amount", 100);
            Scribe_Values.Look(ref data.suspendOnDrop, "suspend_on_drop", false);
            Scribe_Values.Look(ref data.productLimitEnabled, "product_limit_enabled", false);
            Scribe_Defs.Look(ref data.productDef, "product_def");
            Scribe_Values.Look(ref data.productThreshold, "product_threshold", 0);
            Scribe_Values.Look(ref data.countEquipped, "count_equipped", false);
        }
    }
}
