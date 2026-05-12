using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace MGAutoSell.AI
{
    public class JobDriver_ShuttleBarter : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // A = Comms
            // B = Shuttle
            // GOTO Comms
            var autoSell = this;
            autoSell.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            autoSell.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            var failOn = () => pawn.IsPrisoner || pawn.Dead || pawn.IsBrokenDown() ||
                               TargetThingA is Building_CommsConsole { CanUseCommsNow: false } ||
                               (TargetThingA is Pawn { Spawned: false }) ||
                               !Current.Game.GetComponent<TradeRulesGameComp>().tradeRules.Any();
            autoSell.FailOn(failOn);

            yield return Toils_Goto.GotoThing(TargetIndex.A, TargetThingA is Building_CommsConsole ? PathEndMode.InteractionCell : PathEndMode.Touch)
                .FailOn(failOn);

            // WAIT
            var wait = ToilMaker.MakeToil();
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.activeSkill = () => SkillDefOf.Social;
            if (TargetThingA is Building_CommsConsole)
                wait.defaultDuration = 600;
            wait.FailOn(failOn);
            yield return wait.WithProgressBarToilDelay(TargetIndex.B, wait.defaultDuration);


        }
    }
}
