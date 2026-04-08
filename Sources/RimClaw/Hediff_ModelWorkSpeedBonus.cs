using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Hediff_ModelWorkSpeedBonus : Hediff
    {
        private float workSpeedMultiplier = 1f;

        public float WorkSpeedMultiplier => workSpeedMultiplier;

        public void SetWorkSpeedMultiplier(float multiplier)
        {
            workSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workSpeedMultiplier, "workSpeedMultiplier", 1f);
        }

        public override string TipStringExtra
        {
            get
            {
                return $"Model work speed multiplier: x{workSpeedMultiplier:0.00}\nManeuver ability multiplier: x{workSpeedMultiplier:0.00}\nTalking ability multiplier: x{workSpeedMultiplier:0.00}";
            }
        }
    }
}
