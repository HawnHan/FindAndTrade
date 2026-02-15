using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MGAutoSell
{
    [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen))]
    public static class DoTradeOnOpen
    {
        
        public static void Postfix()
        {
            try
            {
                var deal = TradeSession.deal;
                if (deal == null) return;

                TradeDealProcessor.DoTradeDeal(deal);
                
            }
            catch (Exception ex)
            {
                Log.Error($"[BuyEverythingOnOpen] Failed to auto-select tradeables: {ex}");
            }
        }

        
    }

    public record TradeEntry(Tradeable Tradeable, ThingDef ThingDef, int ColonyCount, int TraderCount);


}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}