using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Hediff_InsufficientIORate : Hediff
    {
        public float ThrottleRatio => Mathf.Clamp01(1f - Severity);

        public float WorkSpeedFactor
        {
            get
            {
                float ratio = ThrottleRatio;
                if (ratio >= 0.9f)
                {
                    return 0.9f;
                }

                if (ratio >= 0.8f)
                {
                    return 0.8f;
                }

                if (ratio >= 0.6f)
                {
                    return 0.6f;
                }

                if (ratio >= 0.4f)
                {
                    return 0.4f;
                }

                return 0.2f;
            }
        }

        public void SetThrottleRatio(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            Severity = 1f - clamped;
        }

        public override string TipStringExtra
        {
            get
            {
                return $"I/O throttle severity: {Severity:P0}\nWork speed multiplier: x{WorkSpeedFactor:0.00}";
            }
        }
    }
}