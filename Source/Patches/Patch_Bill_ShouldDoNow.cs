using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace IngredientThreshold.Patches
{
    [HarmonyPatch]
    public static class Patch_Bill_ShouldDoNow
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Bill_Production), "ShouldDoNow");
        }

        public static bool Prefix(Bill __instance, ref bool __result)
        {
            var bill = __instance as Bill_Production;
            if (bill == null)
                return true;

            if (bill.repeatMode != ThresholdRepeatModeDef.IngredientThreshold)
                return true;

            if (bill.suspended)
            {
                __result = false;
                return false;
            }

            if (!Patch_Bill_ExposeData.extraData.TryGetValue(bill, out var data)
                || data.ingredient == null)
            {
                __result = false;
                return false;
            }

            var map = (bill.billStack?.billGiver as Thing)?.Map;
            if (map == null)
            {
                __result = false;
                return false;
            }

            bool aboveThreshold = map.resourceCounter.GetCount(data.ingredient) > data.threshold;
            if (!aboveThreshold && data.suspendOnDrop)
            {
                bool anyPawnWorking = false;
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn.CurJob != null && pawn.CurJob.bill == bill)
                    {
                        anyPawnWorking = true;
                        break;
                    }
                }
                if (!anyPawnWorking)
                    bill.suspended = true;
            }

            __result = aboveThreshold;
            return false;
        }
    }
}
