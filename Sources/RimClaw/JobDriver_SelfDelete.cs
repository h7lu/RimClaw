using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class JobDriver_SelfDelete : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil wait = Toils_General.Wait(RimClawConfig.Values.selfDeleteTicks);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;

            Toil delete = ToilMaker.MakeToil("RimClaw_SelfDelete");
            delete.initAction = delegate
            {
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            };
            delete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return delete;
        }
    }
}
