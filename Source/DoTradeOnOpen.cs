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
        private static MethodInfo _propertySetter = AccessTools.PropertySetter(typeof(Tradeable), nameof(Tradeable.CountToTransfer));
        private static Dictionary<ThingDef, int> ThingDefAggregations = new();
        private static Dictionary<TradeRule, int> TradeRuleAggregations = new();
        public static void Postfix()
        {
            try
            {
                var deal = TradeSession.deal;
                if (deal == null) return;

                var map = TradeSession.playerNegotiator?.Map;
                if(map == null) return;

                var autoTrade = map.GetComponent<TradeRulesMapComp>();

                if (!autoTrade.tradeRules.Any())
                    return;

                // Get tradeables list (property in most versions)
                var tradeables = deal.AllTradeables;
                if (tradeables == null) return;

                bool changedAnything = false;

                var sellDictionary = new List<(Tradeable Tradeable, int Count)>();
                var buyDictionary = new List<(Tradeable Tradeable, int Count)>();
                var itemCache = tradeables.ToList();
                
                ThingDefAggregations.Clear();
                TradeRuleAggregations.Clear();

                ThingDefAggregations.AddRange(itemCache.GroupBy(x => x.ThingDef).ToDictionary(x => x.Key,
                    x => x.ToList().Sum(x => x.CountHeldBy(Transactor.Colony))));

                foreach (var rule in autoTrade.tradeRules)
                {
                    
                    var items = itemCache
                        .Where(x => rule.search.AppliesTo(x.AnyThing))
                        .Select(x => new TradeEntry(x, x.ThingDef, x.CountHeldBy(Transactor.Colony), x.CountHeldBy(Transactor.Trader)))
                        .ToList();

                    var toSell = rule.AllowSell 
                        ? items.Where(x => GetCount(rule, x.ThingDef) > rule.SellDownTo).ToList()
                        : [];
                    toSell.ForEach(x =>
                    {
                        items.Remove(x);
                        itemCache.Remove(x.Tradeable);
                    });

                    sellDictionary.AddRange(toSell.Select(x =>
                    {
                        var sellOrder = (x.Tradeable, Math.Max(GetCount(rule, x.ThingDef) - rule.SellDownTo, x.ColonyCount));
                        AddCount(rule, x.ThingDef, -sellOrder.Item2);
                        return sellOrder;
                    }));

                    var toBuy =
                        rule.AllowBuy
                            ? items
                                .Where(x => 
                                    rule.BuyWhenBelow == 0
                                        ? GetCount(rule, x.ThingDef) < rule.BuyUpTo
                                        : GetCount(rule, x.ThingDef) < rule.BuyWhenBelow)
                                .ToList()
                            : [];
                    toBuy.ForEach(x => itemCache.Remove(x.Tradeable));

                    buyDictionary.AddRange(toBuy.Select(x =>
                    {
                        var buyOrder = (x.Tradeable, Math.Min(rule.BuyUpTo - GetCount(rule, x.ThingDef), x.TraderCount));
                        AddCount(rule, x.ThingDef, buyOrder.Item2);
                        return buyOrder;
                    }));
                }

                foreach (var (tradeable, toSell) in sellDictionary) 
                    SetCount(tradeable, -toSell);

                foreach (var (tradeable, toBuy) in buyDictionary)
                    SetCount(tradeable, toBuy);

                deal.UpdateCurrencyCount();

                // Buying too much
                var buyReversed = buyDictionary.ToList();
                buyReversed.Reverse();
                while (deal.CurrencyTradeable.CountToTransfer < 0 && deal.CurrencyTradeable.CountToTransfer * -1 > deal.CurrencyTradeable.CountHeldBy(Transactor.Colony))
                {
                    var gap = deal.CurrencyTradeable.CountToTransfer * -1 -
                              deal.CurrencyTradeable.CountHeldBy(Transactor.Colony);
                    deal.NormalizeWith(buyReversed, gap);
                }

                // Selling too much
                var sellReversed = sellDictionary.ToList();
                sellReversed.Reverse();
                while (deal.CurrencyTradeable.CountToTransfer > deal.CurrencyTradeable.CountHeldBy(Transactor.Trader))
                {
                    var gap = deal.CurrencyTradeable.CountToTransfer -
                              deal.CurrencyTradeable.CountHeldBy(Transactor.Trader);
                    deal.NormalizeWith(sellReversed, gap);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BuyEverythingOnOpen] Failed to auto-select tradeables: {ex}");
            }
        }

        private static void NormalizeWith(this TradeDeal deal, List<(Tradeable Tradeable, int Count)> list, int gap)
        {
            var item = list.FirstOrDefault();
            var cost = Math.Max(item.Tradeable.CurTotalCurrencyCostForSource,
                item.Tradeable.CurTotalCurrencyCostForDestination);
            if (item.Count == 1 || cost < gap)
            {
                SetCount(item.Tradeable, 0);
                list.Remove(item);
            }
            else
            {
                var costPer = cost / item.Tradeable.CountToTransfer;
                item.Count = item.Tradeable.CountToTransfer - (int)(item.Count < 0 ? Math.Floor(gap/costPer) : Math.Ceiling(gap / costPer));
                SetCount(item.Tradeable, item.Count);
            }
            deal.UpdateCurrencyCount();
        }

        private static void SetCount(Tradeable tradeable, int count)
        {
            _propertySetter.Invoke(tradeable, [count]);

        }

        private static int GetCount(TradeRule rule, ThingDef def)
        {
            return rule.Aggregation switch
            {
                TradeRuleAggregation.ThingDef => ThingDefAggregations.GetValueOrDefault(def, 0),
                TradeRuleAggregation.Rule => TradeRuleAggregations.GetValueOrDefault(rule, 0),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static void AddCount(TradeRule rule, ThingDef def, int amount)
        {
            switch(rule.Aggregation)
            {
                case TradeRuleAggregation.ThingDef:
                    if (!ThingDefAggregations.TryAdd(def, amount))
                        ThingDefAggregations[def] += amount;
                    break;
                case TradeRuleAggregation.Rule:
                    if (!TradeRuleAggregations.TryAdd(rule, amount))
                        TradeRuleAggregations[rule] += amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public record TradeEntry(Tradeable Tradeable, ThingDef ThingDef, int ColonyCount, int TraderCount);


}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}