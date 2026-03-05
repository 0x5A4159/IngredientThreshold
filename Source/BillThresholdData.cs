using Verse;

namespace IngredientThreshold
{
    public class BillThresholdData
    {
        public ThingDef ingredient;
        public int threshold = 128;
        public bool suspendOnDrop = false;
        public bool productLimitEnabled = false;
        public ThingDef productDef;
        public int productThreshold = 0;
        public bool countEquipped = false;
    }
}
