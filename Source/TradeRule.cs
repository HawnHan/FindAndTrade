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
        public QuerySearch Search { get; private set; }
        public IntRangeUB StockRange;


        public void ExposeData()
        {
            var querySearch = Search;
            Scribe_Deep.Look(ref querySearch, "search");
            Scribe_Values.Look(ref StockRange.range, "sel");

            Search = querySearch;
        }

        public TradeRule(string name)
        {
            StockRange = new IntRangeUB(0, 100);
            Search = new QuerySearch()
            {
                name = name,
            };
        }

        public TradeRule()
        {
            
        }
    }
}
