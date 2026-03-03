using HarmonyLib;
using Verse;

namespace IngredientThreshold
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("com.rileydoggy.ingredientthreshold").PatchAll();
        }
    }
}
