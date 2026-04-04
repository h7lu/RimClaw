using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_ClawfishColor : CompProperties
    {
        public CompProperties_ClawfishColor()
        {
            compClass = typeof(CompClawfishColor);
        }
    }

    public class CompClawfishColor : ThingComp
    {
        private bool initialized;
        private Color color = Color.white;

        public bool Initialized => initialized;
        public Color Color => color;

        public void Initialize(Color newColor)
        {
            color = newColor;
            initialized = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", defaultValue: false);
            Scribe_Values.Look(ref color, "color", Color.white);
        }
    }
}