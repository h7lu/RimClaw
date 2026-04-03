using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class Hediff_SelfReprogrammingRegen : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(60))
            {
                return;
            }

            float healPerDay = RimClawConfig.Values.selfRegenHpPerDay;
            float healNow = healPerDay / 1000f;
            var injuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x => !x.IsPermanent()).ToList();
            if (injuries.Count == 0)
            {
                return;
            }

            injuries.RandomElement().Heal(healNow);
        }
    }

    public class JobDriver_SelfReprogramming : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            Toil start = ToilMaker.MakeToil("RimClaw_SelfReprogrammingStart");
            start.initAction = delegate
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_SelfReprogrammingRegen) == null)
                {
                    pawn.health.AddHediff(RimClawDefOf.RimClaw_SelfReprogrammingRegen);
                }
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;

            Toil wait = Toils_General.Wait(RimClawConfig.Values.selfReprogrammingTicks);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;

            Toil end = ToilMaker.MakeToil("RimClaw_SelfReprogrammingEnd");
            end.initAction = delegate
            {
                Hediff regen = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_SelfReprogrammingRegen);
                if (regen != null)
                {
                    pawn.health.RemoveHediff(regen);
                }
            };
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }

    public class MentalState_ContextCollapse : MentalState
    {
        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            forceRecoverAfterTicks = RimClawConfig.Values.contextCollapseDurationTicks;
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (!pawn.IsHashIntervalTick(240, delta) || pawn.jobs == null || pawn.Downed)
            {
                return;
            }

            float roll = Rand.Value;
            if (roll < 0.4f)
            {
                IntVec3 dest = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 12);
                pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.GotoWander, dest), JobTag.Misc);
                return;
            }

            Thing haulable = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                18f);
            if (haulable != null)
            {
                IntVec3 dropCell = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 10);
                Job haul = JobMaker.MakeJob(JobDefOf.HaulToCell, haulable, dropCell);
                haul.count = 1;
                pawn.jobs.TryTakeOrderedJob(haul, JobTag.Misc);
            }
        }
    }

    [HarmonyPatch(typeof(RecipeWorker), nameof(RecipeWorker.AvailableOnNow))]
    public static class Patch_RecipeWorker_AvailableOnNow
    {
        public static void Postfix(RecipeWorker __instance, Thing thing, BodyPartRecord part, ref bool __result)
        {
            if (!__result || thing is not Pawn pawn || !ClawfishUtility.IsClawfish(pawn))
            {
                return;
            }

            RecipeDef recipe = __instance.recipe;
            if (recipe == null || !recipe.IsSurgery)
            {
                return;
            }

            if (recipe.defName == null)
            {
                __result = false;
                return;
            }

            if (RimClawConfig.Values.allowedSurgeryDefNames.Contains(recipe.defName))
            {
                return;
            }

            for (int i = 0; i < RimClawConfig.Values.allowedSurgeryNameContains.Count; i++)
            {
                string contains = RimClawConfig.Values.allowedSurgeryNameContains[i];
                if (!contains.NullOrEmpty() && recipe.defName.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
            }

            __result = false;
        }
    }
}
