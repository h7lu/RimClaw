using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_ClawfishConnection
    {
        [HarmonyPostfix]
        public static void AddClawfishGizmos(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!ClawfishUtility.IsClawfishColonist(__instance))
                return;

            var gizmoList = new List<Gizmo>(__result);
            ClawfishConnectionGizmo.GetGizmosForClawfish(__instance, gizmoList);
            __result = gizmoList;
        }
    }
}
