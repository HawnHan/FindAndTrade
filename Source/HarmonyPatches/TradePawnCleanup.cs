using HarmonyLib;
using Verse;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public static class TradePawnCleanup
    {
        public static void Postfix(Pawn __instance)
        {
            var comp = Current.Game.GetComponent<TradeRulesGameComp>();
            comp.traders.Remove(__instance);
            comp.SellerOverride.RemoveAll(x => x.Value == __instance);
        }
    }
}
