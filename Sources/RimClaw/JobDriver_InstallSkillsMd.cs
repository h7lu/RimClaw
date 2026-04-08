using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class JobDriver_InstallSkillsMd : JobDriver
    {
        private const int InstallDuration = 180; // 3 seconds = 180 ticks

        protected Thing SkillsMdItem
        {
            get
            {
                if (job == null || job.targetA == null || !job.targetA.HasThing)
                {
                    return null;
                }

                return job.targetA.Thing;
            }
        }

        protected Pawn TargetPawn
        {
            get
            {
                if (job == null || job.targetB == null)
                {
                    return null;
                }
                return job.targetB.Thing as Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (SkillsMdItem == null)
            {
                return false;
            }

            return pawn.Reserve(SkillsMdItem, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);

            // Goto the item
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            // Pick up the item
            yield return Toils_General.Do(delegate
            {
                if (SkillsMdItem != null)
                {
                    pawn.carryTracker.TryStartCarry(SkillsMdItem);
                }
            });

            // Pathfind to the target pawn
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);

            // Wait and install
            Toil installToil = Toils_General.Wait(InstallDuration);
            installToil.WithProgressBarToilDelay(TargetIndex.B);
            installToil.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            installToil.FailOnCannotTouch(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return installToil;

            // Perform the install
            yield return Toils_General.Do(delegate
            {
                CompSkillsMd comp = SkillsMdItem?.TryGetComp<CompSkillsMd>();
                if (comp != null && TargetPawn != null)
                {
                    comp.InstallOnPawn(TargetPawn);
                }
            });
        }
    }
}
