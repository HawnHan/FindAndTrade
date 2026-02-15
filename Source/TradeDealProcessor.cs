using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MGAutoSell
{
    [StaticConstructorOnStartup]
    public static class TradeDealProcessor
    {
        private static MethodInfo _propertySetter = AccessTools.PropertySetter(typeof(Tradeable), nameof(Tradeable.CountToTransfer));
        private static Dictionary<ThingDef, int> ThingDefAggregations = new();
        private static Dictionary<ThingDef, int> ExtrasOnMap = new();
        private static Dictionary<TradeRule, int> TradeRuleAggregations = new();

        private static bool GroupedTrading = false;

        public static void StartGroupedTrading()
        {
            if (GroupedTrading)
            {
                Log.Error("Attempted to start a trading session while one is already running");
                return;
            }

            ThingDefAggregations.Clear();
            TradeRuleAggregations.Clear();
            ExtrasOnMap.Clear();
            GroupedTrading = true;
        }

        public static void EndGroupedTrading()
        {
            if (!GroupedTrading)
            {
                Log.Error("Attempted to end a trading session while one is not running.");
                return;
            }

            ThingDefAggregations.Clear();
            TradeRuleAggregations.Clear();
            ExtrasOnMap.Clear();
            GroupedTrading = false;
        }

        [DebugAction("FindAndAutoSell", "DoTrade")]
        public static void DoTrade()
        {
            var pawn = Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
            var trader = pawn.MaxBy(x => x.skills?.GetSkill(SkillDefOf.Social).Level);
            var ships = Find.CurrentMap.passingShipManager.passingShips;
            StartGroupedTrading();
            foreach (var passingShip in ships)
            {
                Log.Message($"Trading with {(passingShip as TradeShip).TraderName}");
                TradeSession.SetupWith(passingShip as TradeShip, trader, false);
                var deal = TradeSession.deal;
                DoTradeDeal(deal);

                var silver = deal.CurrencyTradeable.CountToTransfer;

                var buy = deal.AllTradeables
                    .Where(x => x.CountToTransfer > 0)
                    .Select(x => (x.ThingDef.label, x.CountToTransfer))
                    .ToList();
                var sell = deal.AllTradeables
                    .Where(x => x.CountToTransfer < 0)
                    .Select(x => (x.ThingDef.label, x.CountToTransfer))
                    .ToList();

                if(!buy.Any() && !sell.Any())
                    continue;

                if (!deal.TryExecute(out var actuallyTraded)) 
                    continue;

                if(!actuallyTraded)
                    Debugger.Break();

                DoLetter(passingShip as ITrader, trader, deal, trader.Position, buy, sell, silver);

            }
            EndGroupedTrading();
        }

        public static void DoTradeDeal(TradeDeal deal)
        {
            var map = TradeSession.playerNegotiator?.Map;
            if (map == null) return;

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

            if (!GroupedTrading)
            {
                ThingDefAggregations.Clear();
                TradeRuleAggregations.Clear();
                ExtrasOnMap.Clear();
            }

            var itemsToAdd = itemCache.GroupBy(x => x.ThingDef).ToDictionary(x => x.Key,
                x => x.ToList().Sum(x => x.CountHeldBy(Transactor.Colony)));

            foreach (var item in itemsToAdd)
            {
                ThingDefAggregations.TryAdd(item.Key, item.Value);
            }

            var itemsOnMap = map.listerThings.AllThings.Where(x => !x.IsForbidden(Faction.OfPlayer)).ToList();
            tradeables.ForEach(x => x.thingsColony.ForEach(y => itemsOnMap.Remove(y)));

            var pairings = new Dictionary<ThingDef, TradeRule>();
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
                    pairings.Add(x.ThingDef, rule);
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
                SetTradeCount(tradeable, -toSell);

            foreach (var (tradeable, toBuy) in buyDictionary)
                SetTradeCount(tradeable, toBuy);

            deal.UpdateCurrencyCount();

            // Buying too much
            var buyReversed = buyDictionary.ToList();
            buyReversed.Reverse();
            while (deal.CurrencyTradeable.CountToTransfer < 0 && deal.CurrencyTradeable.CountToTransfer * -1 > deal.CurrencyTradeable.CountHeldBy(Transactor.Colony))
            {
                var gap = deal.CurrencyTradeable.CountToTransfer * -1 -
                          deal.CurrencyTradeable.CountHeldBy(Transactor.Colony);
                deal.NormalizeWith(buyReversed, pairings, gap);
            }

            // Selling too much
            var sellReversed = sellDictionary.ToList();
            sellReversed.Reverse();
            while (deal.CurrencyTradeable.CountToTransfer > deal.CurrencyTradeable.CountHeldBy(Transactor.Trader))
            {
                var gap = deal.CurrencyTradeable.CountToTransfer -
                          deal.CurrencyTradeable.CountHeldBy(Transactor.Trader);
                deal.NormalizeWith(sellReversed, pairings, gap);
            }
        }

        private static void NormalizeWith(this TradeDeal deal, List<(Tradeable Tradeable, int Count)> list, Dictionary<ThingDef, TradeRule> pairings, int gap)
        {
            var item = list.FirstOrDefault();
            var cost = Math.Max(item.Tradeable.CurTotalCurrencyCostForSource,
                item.Tradeable.CurTotalCurrencyCostForDestination);
            if (item.Count == 1 || cost < gap)
            {
                AddCount(pairings[item.Tradeable.ThingDef], item.Tradeable.ThingDef, -1);
                SetTradeCount(item.Tradeable, 0);
                list.Remove(item);
            }
            else
            {
                var costPer = cost / item.Tradeable.CountToTransfer;
                var count = item.Count;
                item.Count = item.Tradeable.CountToTransfer - (int)Math.Round(gap / costPer, 0);
                AddCount(pairings[item.Tradeable.ThingDef], item.Tradeable.ThingDef, count - Math.Abs(item.Count));
                SetTradeCount(item.Tradeable, item.Count);
            }
            deal.UpdateCurrencyCount();
        }

        private static void SetTradeCount(Tradeable tradeable, int count)
        {
            _propertySetter.Invoke(tradeable, [count]);

        }

        private static int GetCount(TradeRule rule, ThingDef def, Dictionary<ThingDef, int> recentlyTraded = null)
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
            int before = 0;
            switch (rule.Aggregation)
            {
                case TradeRuleAggregation.ThingDef:
                    ThingDefAggregations.TryGetValue(def, out before);
                    if (!ThingDefAggregations.TryAdd(def, amount))
                        ThingDefAggregations[def] += amount;
                    Log.Warning($"{def.label} {before}->{ThingDefAggregations[def]}");
                    break;
                case TradeRuleAggregation.Rule:
                    ThingDefAggregations.TryGetValue(def, out before);
                    if (!TradeRuleAggregations.TryAdd(rule, amount))
                        TradeRuleAggregations[rule] += amount;
                    Log.Warning($"{def.label} {before}->{ThingDefAggregations[def]}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static readonly string 
            spent = "MGAutoSell.Spent".Translate(), 
            earned = "MGAutoSell.Earned".Translate(),
            bought = "MGAutoSell.Bought".Translate(),
            sold = "MGAutoSell.Sold".Translate();
        public static void DoLetter(
            ITrader trader,
            Pawn pawn,
            TradeDeal deal,
            IntVec3 location,
            List<(string label, int count)> buy,
            List<(string label, int count)> sell,
            int silver)
        {
            if (!buy.Any() && !sell.Any())
                return;

            var word = silver > 0 ? earned : spent;

            var buyStringBuilder = new StringBuilder();
            buyStringBuilder.AppendLine(bought);
            buyStringBuilder.AppendJoin("\n", buy.Select(x => $"{x.label} x{Math.Abs(x.count)}"));

            var sellStringBuilder = new StringBuilder();
            sellStringBuilder.AppendLine(sold);
            sellStringBuilder.AppendJoin("\n", sell.Select(x => $"{x.label} x{Math.Abs(x.count)}"));

            var traderName = trader?.TraderName ?? "someone";

            var body = "MGAutoSell.Letter".Translate(pawn.Name.ToStringShort, traderName, silver,
                word, buyStringBuilder.ToString(), sellStringBuilder.ToString());

            var globalTargetInfo = new GlobalTargetInfo(location, pawn.Map);
            
            Find.LetterStack.ReceiveLetter("MGAutoSell.LetterTitle".Translate(traderName), body,
                LetterDefOf.PositiveEvent, globalTargetInfo);
        }
    }
}
