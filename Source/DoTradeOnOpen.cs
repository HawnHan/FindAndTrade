using HarmonyLib;
using RimWorld;
using System;
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
                Log.Error($"Failed to process Trade UI: {ex}");
            }
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}