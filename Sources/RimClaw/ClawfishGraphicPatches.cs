using HarmonyLib;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.BodyGraphic), MethodType.Getter)]
    public static class Patch_PawnRenderTree_BodyGraphic
    {
        public static bool Prefix(PawnRenderTree __instance, ref Graphic __result)
        {
            Pawn pawn = __instance?.pawn;
            if (!ClawfishUtility.IsClawfish(pawn))
            {
                return true;
            }

            Graphic graphic = ClawfishUtility.GetClawfishBodyGraphic(pawn);
            if (graphic != null)
            {
                __result = graphic;
                return false;
            }

            return true;
        }
    }
}
