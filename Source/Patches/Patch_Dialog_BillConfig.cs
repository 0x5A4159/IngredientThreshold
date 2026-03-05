using System.Collections.Generic;
using System.Linq;
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
                // I should probably look into a better solution than hard-setting the list, this probably causes compat issues with other qol crafting billrepeatmodes
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

            // Iterate all allowed ingredients for bill (sorts results)
            if (Widgets.ButtonText(ingBtn, ingLabel))
            {
                var opts = new List<FloatMenuOption>();
                if (bill.recipe?.ingredients != null)
                {
                    foreach (var ingredientCount in bill.recipe.ingredients)
                    {
                        foreach (var def in ingredientCount.filter.AllowedThingDefs)
                        {
                            if (bill.ingredientFilter != null && !bill.ingredientFilter.Allows(def))
                                continue;
                            var captured = def;
                            opts.Add(new FloatMenuOption(def.LabelCap, () =>
                            {
                                data.ingredient = captured;
                                _buffers.Remove(bill.GetUniqueLoadID());
                            }));
                        }
                    }
                }

                if (opts.Count > 0)
                    Find.WindowStack.Add(new FloatMenu(opts.OrderBy(o => o.Label).ToList()));
                else
                    Messages.Message("This recipe has no configurable ingredients.",
                        MessageTypeDefOf.RejectInput, false);
            }

            string id = bill.GetUniqueLoadID();
            if (!_buffers.ContainsKey(id))
                _buffers[id] = data.threshold.ToString();

            string buf = _buffers[id];
            Widgets.TextFieldNumeric(numField, ref data.threshold, ref buf, 0f, 9_999_999f);
            _buffers[id] = buf;

            listing.CheckboxLabeled("Enable product limit", ref data.productLimitEnabled);

            if (data.productLimitEnabled)
            {
                Rect row2 = listing.GetRect(30f);
                Rect prodBtn = new Rect(row2.x, row2.y, halfW, row2.height);
                Rect prodField = new Rect(row2.x + halfW + 8f, row2.y, halfW, row2.height);

                string prodLabel = data.productDef != null ? $"AND {data.productDef.LabelCap} <" : "(select product)";

                if (Widgets.ButtonText(prodBtn, prodLabel))
                {
                    var opts = new List<FloatMenuOption>();
                    var noneOpt = new FloatMenuOption("(none)", () =>
                    {
                        data.productDef = null;
                        _buffers.Remove(id + "_prod");
                    });
                    if (bill.recipe?.products != null)
                    {
                        foreach (var product in bill.recipe.products)
                        {
                            var captured = product.thingDef;
                            opts.Add(new FloatMenuOption(captured.LabelCap, () =>
                            {
                                data.productDef = captured;
                                _buffers.Remove(id + "_prod");
                            }));
                        }
                    }
                    var sorted = opts.OrderBy(o => o.Label).ToList();
                    sorted.Insert(0, noneOpt);
                    Find.WindowStack.Add(new FloatMenu(sorted));
                }

                string prodBufKey = id + "_prod";
                if (!_buffers.ContainsKey(prodBufKey))
                    _buffers[prodBufKey] = data.productThreshold.ToString();

                string prodBuf = _buffers[prodBufKey];
                Widgets.TextFieldNumeric(prodField, ref data.productThreshold, ref prodBuf, 0f, 9_999_999f);
                _buffers[prodBufKey] = prodBuf;

                if (data.productDef != null && (data.productDef.IsApparel || data.productDef.IsWeapon))
                    listing.CheckboxLabeled("Count equipped", ref data.countEquipped);
                else
                    data.countEquipped = false;
            }

            listing.CheckboxLabeled("Suspend bill when ingredient drops below threshold", ref data.suspendOnDrop);
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
