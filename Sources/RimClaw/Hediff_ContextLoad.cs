using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Hediff_ContextLoad : Hediff
    {
        private float tokenConsumptionFactor = 1f;
        private float contextCorruptionRate;
        private float contextCollapseFactor = 1f;

        public float TokenConsumptionFactor => Mathf.Max(0.2f, tokenConsumptionFactor);
        public float ContextCorruptionRate => Mathf.Max(0f, contextCorruptionRate);
        public float ContextCollapseFactor => Mathf.Clamp(contextCollapseFactor, 0.2f, 2f);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tokenConsumptionFactor, "tokenConsumptionFactor", 1f);
            Scribe_Values.Look(ref contextCorruptionRate, "contextCorruptionRate", 0f);
            Scribe_Values.Look(ref contextCollapseFactor, "contextCollapseFactor", 1f);
        }

        public void SetLoad(float tokenFactor, float corruptionRate, float collapseFactor)
        {
            tokenConsumptionFactor = Mathf.Max(0.2f, tokenFactor);
            contextCorruptionRate = Mathf.Max(0f, corruptionRate);
            contextCollapseFactor = Mathf.Clamp(collapseFactor, 0.2f, 2f);

            float normalizedToken = Mathf.InverseLerp(1f, 2f, tokenConsumptionFactor);
            float normalizedCorruption = Mathf.Clamp01(contextCorruptionRate / 0.05f);
            Severity = Mathf.Clamp01(normalizedToken * 0.6f + normalizedCorruption * 0.4f);
        }

        public override string TipStringExtra
        {
            get
            {
                return $"Token consumption multiplier: x{TokenConsumptionFactor:0.00}\nContext corruption rate: {ContextCorruptionRate * 100f:0.0}%\nContext collapse factor: x{ContextCollapseFactor:0.00}";
            }
        }
    }
}
