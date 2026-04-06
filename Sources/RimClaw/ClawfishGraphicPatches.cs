using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_PawnRenderer_RenderPawnAt
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef = AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        public static bool Prefix(PawnRenderer __instance, UnityEngine.Vector3 drawLoc, ref Verse.Rot4? rotOverride, bool neverAimWeapon)
        {
            Pawn pawn = __instance == null ? null : PawnRef(__instance);
            Graphic clawfishGraphic = ClawfishUtility.GetDisguisedClawfishGraphic(pawn);
            if (clawfishGraphic != null)
            {
                Verse.Rot4 rot = rotOverride ?? pawn.Rotation;
                clawfishGraphic.Draw(drawLoc, rot, pawn, 0f);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    public static class Patch_ColonistBarColonistDrawer_DrawColonist
    {
        private const float ClawfishPortraitScale = 1.55f;
        private const float PaleClawfishPortraitScale = 1.55f;
        private const float ClawfishPortraitDownwardOffset = 20f;

        private static readonly MethodInfo VanillaGuiDrawTextureMethod =
            AccessTools.Method("UnityEngine.GUI:DrawTexture", new[]
            {
                typeof(Rect),
                typeof(Texture)
            });

        private static readonly MethodInfo ReplacementGuiDrawTextureMethod =
            AccessTools.Method(typeof(Patch_ColonistBarColonistDrawer_DrawColonist), nameof(DrawColonistPortrait));

        private static readonly Dictionary<Pawn, Texture2D> PortraitTextureCache = new Dictionary<Pawn, Texture2D>(new PawnEqualityComparer());
        private static Pawn currentColonist;
        private static bool loggedTranspilerSummary;
        private static bool loggedFirstClawfishPortraitCall;
        private static bool loggedFirstTextureMiss;

        public static void Prefix(Pawn colonist)
        {
            currentColonist = colonist;
        }

        public static void Finalizer()
        {
            currentColonist = null;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int portraitGetCalls = 0;
            int replacedDrawCalls = 0;
            bool sawPortraitGet = false;
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo calledMethod = instruction.operand as MethodInfo;
                if (calledMethod != null && calledMethod.DeclaringType == typeof(PortraitsCache) && calledMethod.Name == nameof(PortraitsCache.Get))
                {
                    portraitGetCalls++;
                    sawPortraitGet = true;
                    yield return instruction;
                    continue;
                }

                if (sawPortraitGet && VanillaGuiDrawTextureMethod != null && instruction.Calls(VanillaGuiDrawTextureMethod))
                {
                    instruction.operand = ReplacementGuiDrawTextureMethod;
                    replacedDrawCalls++;
                    sawPortraitGet = false;
                }

                yield return instruction;
            }

            if (!loggedTranspilerSummary)
            {
                loggedTranspilerSummary = true;
                if (portraitGetCalls == 0)
                {
                    Log.Warning("[RimClaw] DrawColonist transpiler found 0 PortraitsCache.Get calls. Another transpiler likely changed method shape.");
                }
                else if (replacedDrawCalls == 0)
                {
                    Log.Warning($"[RimClaw] DrawColonist transpiler found {portraitGetCalls} PortraitsCache.Get call(s), but replaced 0 GUI.DrawTexture call(s).");
                }
                else
                {
                    Log.Message($"[RimClaw] DrawColonist transpiler active: found {portraitGetCalls} portrait call(s), replaced {replacedDrawCalls} draw call(s).");
                }
            }
        }

        public static void DrawColonistPortrait(Rect rect, Texture vanillaTexture)
        {
            Pawn pawn = currentColonist;
            if (pawn?.def != RimClawDefOf.RimClaw_ClawfishHuman)
            {
                Widgets.DrawTextureFitted(rect, vanillaTexture, 1f);
                return;
            }

            if (!loggedFirstClawfishPortraitCall)
            {
                loggedFirstClawfishPortraitCall = true;
                Log.Message($"[RimClaw] Clawfish portrait hook called for pawn '{pawn?.LabelShort ?? "<null>"}' using path '{ClawfishUtility.GetPortraitSouthTexPath(pawn)}'.");
            }

            if (!PortraitTextureCache.TryGetValue(pawn, out Texture2D clawfishTexture) || clawfishTexture == null)
            {
                clawfishTexture = ContentFinder<Texture2D>.Get(ClawfishUtility.GetPortraitSouthTexPath(pawn), reportFailure: false);
                if (clawfishTexture != null)
                {
                    PortraitTextureCache[pawn] = clawfishTexture;
                }
            }

            if (clawfishTexture == null)
            {
                if (!loggedFirstTextureMiss)
                {
                    loggedFirstTextureMiss = true;
                    Log.Warning($"[RimClaw] Clawfish portrait texture missing at '{ClawfishUtility.GetPortraitSouthTexPath(pawn)}'. Falling back to vanilla portrait.");
                }
                Widgets.DrawTextureFitted(rect, vanillaTexture, 1f);
                return;
            }

            Rect adjustedRect = GetAdjustedClawfishPortraitRect(rect, pawn);
            Color tint = GetPortraitTintColor(pawn);
            Material tintedMaterial = MaterialPool.MatFrom(clawfishTexture, ShaderDatabase.Cutout, tint);
            Widgets.DrawTextureFitted(adjustedRect, clawfishTexture, 1f, Vector2.one, new Rect(0f, 0f, 1f, 1f), 0f, tintedMaterial, 1f);
        }

        private static Color GetPortraitTintColor(Pawn pawn)
        {
            Color source = ClawfishUtility.GetOrAssignColor(pawn);
            Color.RGBToHSV(source, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * 1.2f);
            v = Mathf.Clamp01(v * 1.5f);
            Color brightened = Color.HSVToRGB(h, s, v);
            return Color.Lerp(brightened, Color.white, 0.04f);
        }

        private static Rect GetAdjustedClawfishPortraitRect(Rect baseRect, Pawn pawn)
        {
            float scale = ClawfishPortraitScale;
            Color pawnColor = ClawfishUtility.GetOrAssignColor(pawn);
            //if (pawnColor.grayscale >= 0.78f)
            //{
            //    scale = PaleClawfishPortraitScale;
           // }

            float width = baseRect.width * scale;
            float height = baseRect.height * scale;
            float x = baseRect.x + (baseRect.width - width) * 0.5f;
            float y = baseRect.y + (baseRect.height - height) * 0.5f + ClawfishPortraitDownwardOffset;
            return new Rect(x, y, width, height);
        }
    }
}
