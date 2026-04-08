using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Building_MemoryDisk : Building
    {
        private const string DebugPrefix = "[RimClaw][MemoryDisk][Building]";

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            CompMemoryDisk compMemoryDisk = this.TryGetComp<CompMemoryDisk>();
            Log.Message($"{DebugPrefix} SpawnSetup thing={ThingID} rot={Rotation} respawn={respawningAfterLoad} hasModel={(compMemoryDisk?.HasModel ?? false)} map={(map != null ? map.Index.ToString() : "null")}");
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Log.Message($"{DebugPrefix} DeSpawn thing={ThingID} mode={mode} rot={Rotation}");
            base.DeSpawn(mode);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            CompMemoryDisk compMemoryDisk = this.TryGetComp<CompMemoryDisk>();
            if (compMemoryDisk == null || !compMemoryDisk.HasModel)
            {
                Log.Message($"{DebugPrefix} DrawAt skipped thing={ThingID} flip={flip} hasComp={(compMemoryDisk != null)} hasModel={(compMemoryDisk?.HasModel ?? false)}");
                return;
            }

            CompProperties_MemoryDisk compProperties = compMemoryDisk.Props;
            GraphicData formingGraphicData = compProperties?.formingGraphicData;
            if (formingGraphicData == null)
            {
                Log.Warning($"{DebugPrefix} DrawAt missing formingGraphicData thing={ThingID} compProps={(compProperties != null)}");
                return;
            }

            Vector3 loc = drawLoc + compProperties.GetRotationOffset(Rotation);
            loc.y += compProperties.formingGraphicYOffset;
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            float bob = Mathf.PingPong(ticksGame * compProperties.formingMechBobSpeed, compProperties.formingMechYBobDistance);
            loc.z += bob;
            Log.Message($"{DebugPrefix} DrawAt thing={ThingID} rot={Rotation} ticks={ticksGame} drawLoc={drawLoc} offset={compProperties.GetRotationOffset(Rotation)} yOffset={compProperties.formingGraphicYOffset} bob={bob:0.0000} graphic={formingGraphicData.texPath} size={formingGraphicData.drawSize} hasModel={compMemoryDisk.HasModel}");
            formingGraphicData.Graphic.Draw(loc, Rotation, this, 0f);
        }
    }
}