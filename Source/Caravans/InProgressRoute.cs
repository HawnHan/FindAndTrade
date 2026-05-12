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
    internal class InProgressRoute : MapComponent
    {
        public List<Thing> Things { get; set; }
        public Pawn Trader { get; set; }
        public Building_PassengerShuttle Shuttle { get; set; }
        public Settlement Settlement { get; set; }

        public long RouteStart { get; set; } = 0;

        public InProgressRoute(Map map) : base(map)
        {

        }

        public void ExposeData()
        {
            throw new NotImplementedException();
        }

        public override void MapComponentTick()
        {
            RouteStart++;
        }
    }
}
