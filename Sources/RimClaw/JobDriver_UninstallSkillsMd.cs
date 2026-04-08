using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class JobDriver_UninstallSkillsMd : JobDriver
    {
        private const int UninstallDuration = 180;

        private Pawn TargetPawn
        {
            get
            {
                if (job == null || !job.targetA.HasThing)
                {
                    return null;
                }

                return job.targetA.Thing as Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);

            Toil wait = Toils_General.Wait(UninstallDuration);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;

            Toil uninstall = ToilMaker.MakeToil("RimClaw_UninstallSkillsMd");
            uninstall.initAction = delegate
            {
                Pawn targetPawn = TargetPawn;
                if (targetPawn == null)
                {
                    return;
                }

                SkillsImplantUtility.UninstallSkillsMdAtIndex(targetPawn, job.count);
            };
            uninstall.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return uninstall;
        }
    }
}
