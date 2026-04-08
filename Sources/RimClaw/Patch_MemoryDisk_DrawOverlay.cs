using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [HarmonyPatch(typeof(Graphic), nameof(Graphic.DrawWorker), new[] { typeof(Vector3), typeof(Rot4), typeof(ThingDef), typeof(Thing), typeof(float) })]
    public static class Patch_MemoryDisk_DrawOverlay
    {
        private const string DebugPrefix = "[RimClaw][MemoryDisk][Overlay]";

        public static void Prefix(Graphic __instance, Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (thing == null)
            {
                return;
            }

            if (thing.def?.defName != "RimClaw_MemoryDisk" || !thing.Spawned)
            {
                return;
            }

            Log.Message($"{DebugPrefix} entered DrawWorker thing={thing.ThingID} thingDef={(thingDef != null ? thingDef.defName : "null")} graphic={__instance.GetType().Name} loc={loc} rot={rot} extraRotation={extraRotation:0.###}");

            GraphicData baseGraphicData = thing.def.graphicData;
            if (baseGraphicData?.Graphic != __instance)
            {
                Log.Message($"{DebugPrefix} skipped non-base graphic thing={thing.ThingID} graphic={__instance.GetType().Name} baseGraphic={(baseGraphicData != null ? baseGraphicData.Graphic?.GetType().Name : "null")}");
                return;
            }

            CompMemoryDisk compMemoryDisk = thing.TryGetComp<CompMemoryDisk>();
            if (compMemoryDisk == null || !compMemoryDisk.HasModel)
            {
                Log.Message($"{DebugPrefix} skipped no model thing={thing.ThingID} hasComp={(compMemoryDisk != null)} hasModel={(compMemoryDisk?.HasModel ?? false)}");
                return;
            }

            CompProperties_MemoryDisk compProperties = compMemoryDisk.Props;
            GraphicData formingGraphicData = compProperties?.formingGraphicData;
            if (formingGraphicData == null)
            {
                Log.Warning($"{DebugPrefix} missing formingGraphicData thing={thing.ThingID} compProps={(compProperties != null)}");
                return;
            }

            Vector3 drawLoc = loc + compProperties.GetRotationOffset(rot);
            drawLoc.y += compProperties.formingGraphicYOffset;
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            float bob = Mathf.PingPong(ticksGame * compProperties.formingMechBobSpeed, compProperties.formingMechYBobDistance);
            drawLoc.z += bob;

            Log.Message($"{DebugPrefix} Draw thing={thing.ThingID} rot={rot} ticks={ticksGame} drawLoc={loc} offset={compProperties.GetRotationOffset(rot)} yOffset={compProperties.formingGraphicYOffset} bob={bob:0.0000} graphic={formingGraphicData.texPath} size={formingGraphicData.drawSize} hasModel={compMemoryDisk.HasModel}");
            formingGraphicData.Graphic.Draw(drawLoc, rot, thing, 0f);
        }
    }
}