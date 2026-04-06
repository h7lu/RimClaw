using HarmonyLib;
using RimWorld;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) })]
    public static class Patch_Pawn_GetStatValue_InsufficientIO
    {
        [HarmonyPostfix]
        public static void ApplyInsufficientIOThrottle(Thing thing, StatDef stat, bool applyPostProcess, int cacheStaleAfterTicks, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || stat != StatDefOf.WorkSpeedGlobal)
                return;

            if (!ClawfishUtility.IsClawfishColonist(pawn))
                return;

            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_InsufficientIORate) as Hediff_InsufficientIORate;
            if (hediff != null)
            {
                __result *= hediff.ThrottleRatio;
            }
        }
    }
}
