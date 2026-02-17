using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace MGAutoSell
{
    public static class TradeableExtensions
    {
        public static bool AnyThingNotJunk(this Tradeable tradeable, out Thing item)
        {
            item = tradeable.AnyThing;
            if(item == null)
                return false;

            // Traders don't have a map, and therefore can't designate junk
            if (item.Map?.designationManager.DesignationOn(item, MGDesignatorDefOf.MGAutoSell) == null)
                return true;

            item = tradeable.FirstThingTrader;
            if(item != null)
                return true;

            var designator = MGDesignatorDefOf.MGAutoSell;
            item = tradeable.thingsColony.FirstOrDefault(x =>
                x.Map.designationManager.DesignationOn(x, designator) == null);

            return item != null;
        }
    }
}
