using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MGAutoSell.Caravans
{
    public class MapCacheTracker(Map map) : MapComponent(map)
    {
        public override void MapRemoved() => 
            Current.Game.GetComponent<TradeRulesGameComp>().SellCache.Remove(map);

        public override void MapGenerated() => 
            Current.Game.GetComponent<TradeRulesGameComp>().Fetch(map);
    }
}
