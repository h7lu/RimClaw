using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Hediff_InsufficientIORate : Hediff
    {
        private float throttleRatio = 1f; // 0 = no tokens (full debuff), 1 = full supply (no debuff)

        public float ThrottleRatio => throttleRatio;

        public void SetThrottleRatio(float ratio)
        {
            throttleRatio = Mathf.Clamp01(ratio);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SetThrottleRatio(0f); // Start with no supply
        }

        public override void Tick()
        {
            base.Tick();
        }
    }
}

