using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public static class RimClawGlowUtility
    {
        public static void DrawGlow(Vector3 drawPos, Color color, float radius)
        {
            Color outerTint = new Color(color.r, color.g, color.b, 0.18f);
            Color innerTint = new Color(color.r, color.g, color.b, 0.34f);
            Material outerMat = SolidColorMaterials.SimpleSolidColorMaterial(outerTint);
            Material innerMat = SolidColorMaterials.SimpleSolidColorMaterial(innerTint);
            Vector3 pos = drawPos;
            pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();

            // MeshPool.plane10 spans ~10 cells at scale 1, so convert world radius to local scale.
            float outerScale = Mathf.Max(0.05f, (radius * 2f) / 10f);
            float innerScale = Mathf.Max(0.05f, (radius * 1.25f) / 10f);

            Matrix4x4 outer = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(outerScale, 1f, outerScale));
            Matrix4x4 inner = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(innerScale, 1f, innerScale));
            Graphics.DrawMesh(MeshPool.plane10, outer, outerMat, 0);
            Graphics.DrawMesh(MeshPool.plane10, inner, innerMat, 0);
        }

        public static Color SoftenToGlow(Color source)
        {
            Color.RGBToHSV(source, out float h, out _, out _);
            return Color.HSVToRGB(h, 0.15f, 1f);
        }

        public static void SpawnPulseGlow(Thing thing, Color color, float radius)
        {
            if (thing?.Spawned != true || thing.MapHeld == null)
            {
                return;
            }

            ThingDef glowDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Glow") ?? DefDatabase<ThingDef>.GetNamedSilentFail("MoteGlow");
            if (glowDef == null)
            {
                return;
            }

            float moteScale = Mathf.Max(0.5f, radius * 0.35f);
            Mote mote = MoteMaker.MakeAttachedOverlay(thing, glowDef, Vector3.zero, moteScale, -1f);
            if (mote != null)
            {
                mote.instanceColor = new Color(color.r, color.g, color.b, 0.9f);
            }
        }
    }
}
