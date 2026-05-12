using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using MGAutoSell.Caravans;
using RimWorld.Planet;
using Verse;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    public static class SettlementTradeGizmo
    {
        private static string _automaticallyTradeLabel, _automaticallyTradeDescription;
        public static void Postfix(Settlement __instance, ref IEnumerable<Gizmo> __result)
        {
            _automaticallyTradeLabel ??= "MGAutoSell.Caravan.AutomaticallyTradeLabel".Translate();
            _automaticallyTradeDescription ??= "MGAutoSell.Caravan.AutomaticallyTradeDescription".Translate(); 
            var comp = Find.World.GetComponent<SettlementTracker>();
            if (__instance.CanTradeNow)
                __result = __result.AddItem(new Command_Toggle()
                {
                    defaultLabel = _automaticallyTradeLabel,
                    defaultDesc = _automaticallyTradeDescription,
                    isActive = () => comp.Settlements.Contains(__instance),
                    toggleAction = () =>
                    {
                        if (!comp.Settlements.Add(__instance)) 
                            comp.Settlements.Remove(__instance);
                    },
                });
        }
    }
}
