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
                Pawn fisher = __instance.pawn;
                if (fisher?.Map == null || !Rand.Chance(RimClawSettings.FishingSpawnChance))
                {
                    return;
                }

                Pawn clawfish = PawnGenerator.GeneratePawn(RimClawDefOf.RimClaw_ClawfishKind, null);
                ClawfishUtility.ApplyBaseline(clawfish);

                IntVec3 spawnCell = FindSpawnCellNear(fisher);
                GenSpawn.Spawn(clawfish, spawnCell, fisher.Map, WipeMode.Vanish);
                fisher.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                Find.LetterStack.ReceiveLetter(
                    "Clawfish Hooked",
                    $"{fisher.LabelShortCap} hooked a Clawfish and aborted fishing.",
                    LetterDefOf.NeutralEvent,
                    clawfish);
            });
        }

        private static IntVec3 FindSpawnCellNear(Pawn fisher)
        {
            if (CellFinder.TryFindRandomCellNear(fisher.Position, fisher.Map, RimClawSettings.SpawnRadiusFromFisher,
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

}
