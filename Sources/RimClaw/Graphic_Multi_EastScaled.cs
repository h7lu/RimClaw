using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Graphic_Multi_EastScaled : Graphic_Multi
    {
        private const float EastScale = 1.0f;

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            Vector2 original = drawSize;
            if (rot == Rot4.East)
            {
                drawSize = original * EastScale;
            }

            base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
            drawSize = original;
        }
    }
}
