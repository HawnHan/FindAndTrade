using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using MGAutoSell.Caravans;
using RimWorld;
using Verse;

namespace MGAutoSell.HarmonyPatches
{
    [HarmonyPatch]
    public static class ShuttleTradeGizmo
    {
        [HarmonyPatch(typeof(Building_PassengerShuttle), nameof(Building_PassengerShuttle.GetGizmos))]
        [HarmonyPostfix]
        public static void Gizmo(Building_PassengerShuttle __instance, ref IEnumerable<Gizmo> __result)
        {
            var comp = Find.World.GetComponent<SettlementTracker>();
            if (__instance.Faction == Faction.OfPlayer)
                __result = __result.AddItem(new Command_Toggle()
                {
                    isActive = () => comp.Shuttles.Contains(__instance),
                    toggleAction = () =>
                    {
                        if (!comp.Shuttles.Add(__instance))
                            comp.Shuttles.Remove(__instance);
                    }
                });
        }

        [HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
        [HarmonyPostfix]
        public static void Destroy(Building __instance)
        {
            if (__instance is not Building_PassengerShuttle shuttle)
                return;

            var comp = Find.World.GetComponent<SettlementTracker>();    
            comp.Shuttles.Remove(shuttle);
        }
    }
}
