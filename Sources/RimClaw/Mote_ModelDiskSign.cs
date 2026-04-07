using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Mote_ModelDiskSign : MoteAttached
    {
        public float bobAmplitudeTiles = 0.1f;
        public float bobPeriodSeconds = 4f;

        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);

            if (!Spawned || Map == null)
            {
                return;
            }

            float periodSeconds = Mathf.Max(0.01f, bobPeriodSeconds);
            float ticksPerPeriod = periodSeconds * 60f;
            float phase = ticksPerPeriod <= 0f ? 0f : ((Find.TickManager?.TicksGame ?? 0) % ticksPerPeriod) / ticksPerPeriod;
            float bob = Mathf.Sin(phase * Mathf.PI * 2f) * bobAmplitudeTiles;

            exactPosition += new Vector3(0f, bob, 0f);
            Position = exactPosition.ToIntVec3();
        }
    }
}