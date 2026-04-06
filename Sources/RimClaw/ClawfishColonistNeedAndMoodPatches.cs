using HarmonyLib;
using RimWorld;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Patch_PawnNeedsTracker_ShouldHaveNeed
    {
        private static readonly AccessTools.FieldRef<Pawn_NeedsTracker, Pawn> PawnRef = AccessTools.FieldRefAccess<Pawn_NeedsTracker, Pawn>("pawn");

        public static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
        {
            Pawn pawn = __instance == null ? null : PawnRef(__instance);
            if (!ClawfishUtility.IsClawfishColonist(pawn))
            {
                return;
            }

            if (nd == NeedDefOf.Food || nd == NeedDefOf.Rest || nd?.defName == "Joy" || nd?.defName == "Play")
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.NeedsTrackerTickInterval))]
    public static class Patch_PawnNeedsTracker_NeedsTrackerTickInterval
    {
        private static readonly AccessTools.FieldRef<Pawn_NeedsTracker, Pawn> PawnRef = AccessTools.FieldRefAccess<Pawn_NeedsTracker, Pawn>("pawn");

        public static void Postfix(Pawn_NeedsTracker __instance, int delta)
        {
            Pawn pawn = __instance == null ? null : PawnRef(__instance);
            if (!ClawfishUtility.IsClawfishColonist(pawn))
            {
                return;
            }

            if (pawn.IsHashIntervalTick(600, delta))
            {
                __instance.AddOrRemoveNeedsAsAppropriate();
                ClawfishUtility.EnsureAiTraitForLlmAgent(pawn);
                ClawfishUtility.EnsureSkillFloor(pawn, 8);
            }
        }
    }

    [HarmonyPatch(typeof(Need_Mood), nameof(Need_Mood.NeedInterval))]
    public static class Patch_NeedMood_NeedInterval
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawnRef = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        public static void Postfix(Need_Mood __instance)
        {
            Pawn pawn = __instance == null ? null : NeedPawnRef(__instance);
            if (!ClawfishUtility.HasAiMoodLock(pawn))
            {
                return;
            }

            __instance.CurLevel = 0.5f;
        }
    }

    [HarmonyPatch(typeof(Need_Mood), "get_CurInstantLevel")]
    public static class Patch_NeedMood_CurInstantLevel
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawnRef = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        public static void Postfix(Need_Mood __instance, ref float __result)
        {
            Pawn pawn = __instance == null ? null : NeedPawnRef(__instance);
            if (ClawfishUtility.HasAiMoodLock(pawn))
            {
                __result = 0.5f;
            }
        }
    }
}
