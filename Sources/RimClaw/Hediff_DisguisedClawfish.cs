using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Hediff_DisguisedClawfish : HediffWithComps
    {
        private Color displayColor = Color.white;
        private bool initialized;

        public Color DisplayColor => displayColor;

        public void SetColor(Color color)
        {
            displayColor = color;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref displayColor, "displayColor", Color.white);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!initialized)
            {
                displayColor = Color.white;
                initialized = true;
            }
        }
    }
}
