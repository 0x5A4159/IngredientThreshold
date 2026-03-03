using RimWorld;
using Verse;

namespace IngredientThreshold
{
    [DefOf]
    public static class ThresholdRepeatModeDef
    {
        public static BillRepeatModeDef IngredientThreshold;

        static ThresholdRepeatModeDef()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThresholdRepeatModeDef));
        }
    }
}
