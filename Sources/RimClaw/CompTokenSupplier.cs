using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimClaw
{
    public class CompTokenSupplier : ThingComp
    {
        protected List<Pawn> connectedClawfish = new List<Pawn>();
        protected float totalTokenCapacityPerSecond = 400f; // tokens/s max
        protected List<Pawn> gpuAssignments = new List<Pawn>(); // For Host Computer multi-GPU support

        public virtual bool IsValidSupplier => parent != null && !parent.Destroyed;

        public List<Pawn> ConnectedClawfish => connectedClawfish;
        public float TotalTokenCapacity => totalTokenCapacityPerSecond;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref connectedClawfish, "connectedClawfish", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && connectedClawfish == null)
            {
                connectedClawfish = new List<Pawn>();
            }
        }

        public virtual void AddConnectedClaw(Pawn claw)
        {
            if (!connectedClawfish.Contains(claw))
            {
                connectedClawfish.Add(claw);
            }
        }

        public virtual void RemoveConnectedClaw(Pawn claw)
        {
            connectedClawfish.Remove(claw);
        }

        public virtual float GetAvailableTokenRateForClawfish(Pawn claw)
        {
            if (!connectedClawfish.Contains(claw))
                return 0f;

            // Simple distribution: total capacity / number of connected claws
            if (connectedClawfish.Count == 0)
                return totalTokenCapacityPerSecond;

            return totalTokenCapacityPerSecond / connectedClawfish.Count;
        }

        public float GetTotalConsumptionRate()
        {
            float total = 0f;
            for (int i = 0; i < connectedClawfish.Count; i++)
            {
                var comp = connectedClawfish[i].TryGetComp<CompClawfishTokenConnection>();
                if (comp != null)
                {
                    total += comp.GetAdjustedTokenConsumptionRate(connectedClawfish[i]);
                }
            }
            return total;
        }

        public override string CompInspectStringExtra()
        {
            float consumed = GetTotalConsumptionRate();
            float available = GetAvailableTokenRateForClawfish(null); // Average per claw

            return "RimClaw_TokenSupplier_Inspect".Translate(connectedClawfish.Count, totalTokenCapacityPerSecond.ToString("F0"), consumed.ToString("F2"), available.ToString("F2"));
        }
    }
}
