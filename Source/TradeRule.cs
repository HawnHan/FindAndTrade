using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;

namespace MGAutoSell
{
    public class TradeRule : IExposable, IQuerySearch
    {
        public int BuyUpTo;
        public int SellDownTo;
        public bool Enabled = true;
        public TradeRuleAggregation Aggregation = TradeRuleAggregation.ThingDef;
        public QuerySearch search;
        public QuerySearch Search => search;

        public bool AllowSell => SellDownTo > 0 || NoConfig;
        public bool AllowBuy => BuyUpTo > 0;

        public bool NoConfig => SellDownTo == 0 && BuyUpTo == 0;
        public bool Invalid => (SellDownTo > 0 && BuyUpTo > 0 && BuyUpTo > SellDownTo) || BuyUpTo < 0 || SellDownTo < 0;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref search, nameof(search));
            Scribe_Values.Look(ref BuyUpTo, nameof(BuyUpTo));
            Scribe_Values.Look(ref SellDownTo, nameof(SellDownTo));
            Scribe_Values.Look(ref Enabled, nameof(Enabled));
            Scribe_Values.Look(ref Aggregation, nameof(Aggregation));
        }

        public TradeRule(string name)
        {
            search = new QuerySearch()
            {
                name = name,
            };
        }

        public TradeRule()
        {
            
        }
    }
}
