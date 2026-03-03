using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace IngredientThreshold.Patches
{
    [HarmonyPatch(typeof(BillRepeatModeUtility), "MakeConfigFloatMenu")]
    public static class Patch_BillRepeatModeUtility
    {
        public static bool Prefix(Bill_Production bill)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(BillRepeatModeDefOf.RepeatCount.LabelCap,
                    () => bill.repeatMode = BillRepeatModeDefOf.RepeatCount),
                new FloatMenuOption(BillRepeatModeDefOf.TargetCount.LabelCap,
                    () => bill.repeatMode = BillRepeatModeDefOf.TargetCount),
                new FloatMenuOption(BillRepeatModeDefOf.Forever.LabelCap,
                    () => bill.repeatMode = BillRepeatModeDefOf.Forever),
                new FloatMenuOption(ThresholdRepeatModeDef.IngredientThreshold.LabelCap,
                    () => bill.repeatMode = ThresholdRepeatModeDef.IngredientThreshold)
            };

            Find.WindowStack.Add(new FloatMenu(options));
            return false;
        }
    }

    [HarmonyPatch(typeof(Listing_Standard), "EndSection")]
    public static class Patch_Listing_EndSection
    {
        internal static Bill_Production pendingBill;

        public static void Prefix(Listing_Standard listing)
        {
            if (pendingBill == null)
                return;

            var bill = pendingBill;
            pendingBill = null;

            if (bill.repeatMode != ThresholdRepeatModeDef.IngredientThreshold)
                return;

            DrawThresholdControls(listing, bill);
        }

        private static readonly Dictionary<string, string> _buffers =
            new Dictionary<string, string>();

        private static void DrawThresholdControls(
            Listing_Standard listing, Bill_Production bill)
        {
            var data = Patch_Bill_ExposeData.extraData.GetOrCreateValue(bill);

            Rect row = listing.GetRect(30f);
            float halfW = (row.width - 8f) / 2f;
            Rect ingBtn = new Rect(row.x, row.y, halfW, row.height);
            Rect numField = new Rect(row.x + halfW + 8f, row.y, halfW, row.height);

            string ingLabel = data.ingredient != null ? $"If {data.ingredient.LabelCap} >" : "(select ingredient)";

            if (Widgets.ButtonText(ingBtn, ingLabel))
            {
                var opts = new List<FloatMenuOption>();
                if (bill.ingredientFilter != null)
                {
                    foreach (var def in bill.ingredientFilter.AllowedThingDefs)
                    {
                        var captured = def;
                        opts.Add(new FloatMenuOption(def.LabelCap, () =>
                        {
                            data.ingredient = captured;
                            _buffers.Remove(bill.GetUniqueLoadID());
                        }));
                    }
                }

                if (opts.Count > 0)
                    Find.WindowStack.Add(new FloatMenu(opts));
                else
                    Messages.Message("Configure the bill's ingredient filter first.",
                        MessageTypeDefOf.RejectInput, false);
            }

            string id = bill.GetUniqueLoadID();
            if (!_buffers.ContainsKey(id))
                _buffers[id] = data.threshold.ToString();

            string buf = _buffers[id];
            Widgets.TextFieldNumeric(numField, ref data.threshold, ref buf, 0f, 9_999_999f);
            _buffers[id] = buf;
        }
    }

    [HarmonyPatch(typeof(Dialog_BillConfig), "DoWindowContents")]
    public static class Patch_Dialog_BillConfig_Flag
    {
        public static void Prefix(Dialog_BillConfig __instance)
        {
            var bill = Traverse.Create(__instance)
                .Field("bill").GetValue<Bill_Production>();
            Patch_Listing_EndSection.pendingBill = bill;
        }
    }
}
