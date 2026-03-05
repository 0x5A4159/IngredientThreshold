using System.Linq;
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

        private class BillCache
        {
            // Tick Caches

            // ShouldDo spam prevent
            public int lastScanTick = -1;
            public bool anyPawnWorking = false;

            // Check all items in buildings etc for product limit
            public int lastCountTick = -1;
            public int cachedProductCount = 0;

            // Unfinished items tick cache
            public int lastUftTick = -1;
            public bool cachedHasUft = false;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bill_Production, BillCache> _billCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Bill_Production, BillCache>();

        // Tick cache intervals
        private const int PawnScanIntervalTicks = 20;
        private const int ProductCountIntervalTicks = 30;

        private static int CountProductOnMap(Map map, ThingDef def, bool countEquipped)
        {
            int count = 0;

            // Check to see if there are any things in the stockpiles or other ground/map storage
            var mapThings = map.listerThings.ThingsOfDef(def);
            if (mapThings != null)
                foreach (var t in mapThings)
                    count += t.stackCount;

            // Check things in buildings eg wooden dummies
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (!(building is IThingHolder holder)) continue;
                var inner = holder.GetDirectlyHeldThings();
                if (inner == null) continue;
                foreach (var t in inner)
                    if (t.def == def)
                        count += t.stackCount;
            }

            // Check for pawn carrying/hauling/equipped
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                foreach (var t in pawn.inventory.innerContainer)
                    if (t.def == def)
                        count += t.stackCount;

                foreach (var t in pawn.carryTracker.innerContainer)
                    if (t.def == def)
                        count += t.stackCount;

                if (countEquipped)
                {
                    if (pawn.apparel != null)
                        foreach (var apparel in pawn.apparel.WornApparel)
                            if (apparel.def == def)
                                count++;

                    if (pawn.equipment?.Primary != null && pawn.equipment.Primary.def == def)
                        count++;
                }
            }

            return count;
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

            // If there's an unfinished bill but we don't have enough material, we might as well have the pawn finish the unfinished item
            // A thought: might be unintuitive to players? Someone might see the bill still active despite the conditions not being met
            if (bill.recipe?.unfinishedThingDef != null)
            {
                var map2 = (bill.billStack?.billGiver as Thing)?.Map;
                if (map2 != null)
                {
                    var uftCache = _billCache.GetOrCreateValue(bill);
                    int uftTick = Find.TickManager.TicksGame;
                    if (uftTick - uftCache.lastUftTick >= PawnScanIntervalTicks)
                    {
                        uftCache.cachedHasUft = false;
                        var uftList = map2.listerThings.ThingsOfDef(bill.recipe.unfinishedThingDef);
                        if (uftList != null)
                        {
                            foreach (var thing in uftList)
                            {
                                if (thing is UnfinishedThing uft && uft.BoundBill == bill)
                                {
                                    uftCache.cachedHasUft = true;
                                    break;
                                }
                            }
                        }
                        uftCache.lastUftTick = uftTick;
                    }
                    if (uftCache.cachedHasUft)
                    {
                        __result = true;
                        return false;
                    }
                }
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

            var cache = _billCache.GetOrCreateValue(bill);
            int currentTick = Find.TickManager.TicksGame;

            bool ingredientOk = map.resourceCounter.GetCount(data.ingredient) > data.threshold;
            if (!ingredientOk && data.suspendOnDrop)
            {
                if (currentTick - cache.lastScanTick >= PawnScanIntervalTicks)
                {
                    cache.anyPawnWorking = false;
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        if (pawn.CurJob != null && pawn.CurJob.bill == bill)
                        {
                            cache.anyPawnWorking = true;
                            break;
                        }
                    }
                    cache.lastScanTick = currentTick;
                }
                if (!cache.anyPawnWorking)
                    bill.suspended = true;
            }

            bool productOk = true;
            if (data.productLimitEnabled && data.productDef != null)
            {
                if (currentTick - cache.lastCountTick >= ProductCountIntervalTicks)
                {
                    cache.cachedProductCount = CountProductOnMap(map, data.productDef, data.countEquipped);
                    cache.lastCountTick = currentTick;
                }
                productOk = cache.cachedProductCount < data.productThreshold;
            }

            __result = ingredientOk && productOk;
            return false;
        }
    }
}
