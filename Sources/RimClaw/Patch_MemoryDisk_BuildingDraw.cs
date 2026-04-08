using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(ThingWithComps), "DrawAt")]
    public static class Patch_MemoryDisk_BuildingDraw
    {
        private const string DebugPrefix = "[RimClaw][MemoryDisk][DrawProbe]";

        public static void Postfix(ThingWithComps __instance, Vector3 drawLoc, bool flip)
        {
            if (!(__instance is Building) || __instance == null || __instance.def?.defName != "RimClaw_MemoryDisk")
            {
                return;
            }

            CompMemoryDisk compMemoryDisk = __instance.TryGetComp<CompMemoryDisk>();
            Log.Message($"{DebugPrefix} ThingWithComps.DrawAt thing={__instance.ThingID} flip={flip} rot={__instance.Rotation} hasComp={(compMemoryDisk != null)} hasModel={(compMemoryDisk?.HasModel ?? false)} model={(compMemoryDisk?.ModelName ?? "(null)")} drawLoc={drawLoc}");

            if (compMemoryDisk == null || !compMemoryDisk.HasModel)
            {
                return;
            }

            CompProperties_MemoryDisk compProperties = compMemoryDisk.Props;
            GraphicData formingGraphicData = compProperties?.formingGraphicData;
            if (formingGraphicData == null)
            {
                Log.Warning($"{DebugPrefix} missing formingGraphicData thing={__instance.ThingID} compProps={(compProperties != null)}");
                return;
            }

            Vector3 loc = drawLoc + compProperties.GetRotationOffset(__instance.Rotation);
            loc.y += compProperties.formingGraphicYOffset;
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            float bob = Mathf.PingPong(ticksGame * compProperties.formingMechBobSpeed, compProperties.formingMechYBobDistance);
            loc.z += bob;
            Log.Message($"{DebugPrefix} render thing={__instance.ThingID} rot={__instance.Rotation} ticks={ticksGame} drawLoc={drawLoc} offset={compProperties.GetRotationOffset(__instance.Rotation)} yOffset={compProperties.formingGraphicYOffset} bob={bob:0.0000} graphic={formingGraphicData.texPath} size={formingGraphicData.drawSize} hasModel={compMemoryDisk.HasModel}");
            formingGraphicData.Graphic.Draw(loc, __instance.Rotation, __instance, 0f);
        }
    }
}