using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        public static void Postfix(ref Pawn __result)
        {
            if (__result == null || !ClawfishUtility.IsClawfish(__result))
            {
                return;
            }

            ClawfishUtility.ApplyBaseline(__result);
        }
    }

    [HarmonyPatch(typeof(JobDriver_Fish), "CompleteFishingToil")]
    public static class Patch_JobDriver_Fish_CompleteFishingToil
    {
        public static void Postfix(JobDriver_Fish __instance, ref Toil __result)
        {
            if (__result == null)
            {
                return;
            }

            __result.initAction = (Action)Delegate.Combine(__result.initAction, (Action)delegate
            {
                var cfg = RimClawConfig.Values;
                Pawn fisher = __instance.pawn;
                if (fisher?.Map == null || !Rand.Chance(cfg.fishingSpawnChance))
                {
                    return;
                }

                Pawn clawfish = PawnGenerator.GeneratePawn(RimClawDefOf.RimClaw_ClawfishKind, null);
                ClawfishUtility.ApplyBaseline(clawfish);

                IntVec3 spawnCell = FindSpawnCellNear(fisher);
                GenSpawn.Spawn(clawfish, spawnCell, fisher.Map, WipeMode.Vanish);
                fisher.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                Messages.Message(
                    string.Format(cfg.fishingLetterText, fisher.LabelShortCap),
                    clawfish,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            });
        }

        private static IntVec3 FindSpawnCellNear(Pawn fisher)
        {
            if (CellFinder.TryFindRandomCellNear(fisher.Position, fisher.Map, RimClawConfig.Values.spawnRadiusFromFisher,
                c => c.Standable(fisher.Map) && c.GetFirstPawn(fisher.Map) == null,
                out IntVec3 result))
            {
                return result;
            }

            return fisher.Position;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Tame), nameof(WorkGiver_Tame.JobOnThing))]
    public static class Patch_WorkGiver_Tame_JobOnThing
    {
        public static bool Prefix(Thing t, ref Job __result)
        {
            if (t is Pawn pawn && ClawfishUtility.IsClawfish(pawn))
            {
                __result = null;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.FinalizeValue))]
    public static class Patch_StatWorker_FinalizeValue_ModelWorkSpeed
    {
        public static void Postfix(StatWorker __instance, StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null || !ClawfishUtility.IsClawfish(pawn))
            {
                return;
            }

            StatDef statDef = Traverse.Create(__instance).Field("stat").GetValue<StatDef>();
            if (statDef == null)
            {
                return;
            }

            bool modelAffectedStat = statDef.defName == "WorkSpeedGlobal"
                || statDef.defName == "Maneuver"
                || statDef.defName == "SocialImpact"
                || statDef.defName == "NegotiationAbility";
            bool skillsAffectedStat = statDef.defName == "MoveSpeed"
                || statDef.defName == "WorkSpeedGlobal"
                || statDef.defName == "SocialImpact"
                || statDef.defName == "NegotiationAbility";

            if (!modelAffectedStat && !skillsAffectedStat)
            {
                return;
            }

            if (modelAffectedStat)
            {
                Hediff_ModelWorkSpeedBonus hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ModelWorkSpeedBonus) as Hediff_ModelWorkSpeedBonus;
                if (hediff != null)
                {
                    val *= hediff.WorkSpeedMultiplier;
                }
            }

            if (skillsAffectedStat)
            {
                val *= SkillsImplantUtility.GetAdditiveStatFactor(pawn, statDef.defName);
            }
        }
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculateCapacityLevel))]
    public static class Patch_PawnCapacityUtility_CalculateCapacityLevel_ModelWorkSpeed
    {
        public static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            Pawn pawn = diffSet?.pawn;
            if (pawn == null || !ClawfishUtility.IsClawfish(pawn) || capacity == null)
            {
                return;
            }

            // Apply model bonus to maneuver-like capacities and talking capacity.
            if (capacity.defName != "Moving" && capacity.defName != "Manipulation" && capacity.defName != "Talking")
            {
                return;
            }

            Hediff_ModelWorkSpeedBonus hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ModelWorkSpeedBonus) as Hediff_ModelWorkSpeedBonus;
            if (hediff != null)
            {
                __result *= hediff.WorkSpeedMultiplier;
            }
        }
    }

}
