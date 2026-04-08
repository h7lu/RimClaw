using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class JobDriver_InsertModelIntoDisk : JobDriver
    {
        private const int InsertTicks = 180;

        private Thing DiskThing => job.targetA.Thing;
        private Thing PackageThing => job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(DiskThing, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(PackageThing, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnForbidden(TargetIndex.B);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil wait = Toils_General.Wait(InsertTicks);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;

            Toil insert = ToilMaker.MakeToil("RimClaw_InsertModelIntoDisk");
            insert.initAction = delegate
            {
                Thing disk = DiskThing;
                if (disk == null)
                {
                    return;
                }

                CompMemoryDisk comp = disk.TryGetComp<CompMemoryDisk>();
                if (comp == null)
                {
                    return;
                }

                Thing carried = pawn.carryTracker?.CarriedThing;
                if (carried == null)
                {
                    return;
                }

                bool stored = comp.TryStoreModelFromCardThing(carried, consumeThing: true);
                if (!stored)
                {
                    Messages.Message("RimClaw_MemoryDisk_InsertFailed".Translate(), disk, MessageTypeDefOf.RejectInput, historical: false);
                }
            };
            insert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insert;
        }
    }
}
