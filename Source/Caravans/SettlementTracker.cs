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
    /*
     * Every hour, check settlements that are within range and still have money
     * Do work at the comms
     * Work out the exact trade ahead of time, and pack those items and silver
     * Once loaded, trader finds work to get in
     *
     */
    public class SettlementTracker(World world) : WorldComponent(world)
    {
        public HashSet<Settlement> Settlements = [];
        public HashSet<Building_PassengerShuttle> Shuttles = [];

        private readonly Dictionary<(Map, Settlement), float> _distanceCache = new();

        private int nextTick = 0;

        public Dictionary<Settlement, float> GetDistances(Map map, Pawn trader = null, Func<PlanetLayer, float> maxDistance = null, Predicate<Settlement> settlementPredicate = null)
        {
            var distances = new Dictionary<Settlement, float>();

            var settlements = Settlements.ToList();
            if (settlementPredicate != null) 
                settlements.RemoveAll(settlementPredicate);

            if(!settlements.Any())
                return distances;

            foreach (var settlement in settlements)
            {
                if(trader is not null && !trader.CanTradeWith(settlement.Faction, settlement.trader.TraderKind))
                    continue;

                if (!_distanceCache.TryGetValue((map, settlement), out var distance))
                {
                    distance = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, settlement.Tile);
                    _distanceCache[(map, settlement)] = distance;
                }
                if(distance > maxDistance?.Invoke(settlement.Tile.Layer))
                    continue;

                distances[settlement] = distance;
            }
            return distances;
        }

        public Building_PassengerShuttle GetBestForMap(Map map)
        {
            var shuttles = Shuttles
                .Where(x => x.Map == map)
                .ToList();

            if(!shuttles.Any())
                return null;

            return shuttles
                .OrderByDescending(x => x.HitPoints >= x.def.BaseMaxHitPoints)
                .ThenByDescending(x => x.FuelLevel)
                .FirstOrDefault();

        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Settlements, "settlements", LookMode.Reference);
            Scribe_Collections.Look(ref Shuttles, "shuttles", LookMode.Reference);

            Settlements ??= [];
            Shuttles ??= [];
        }


    }
}
