using RimWorld;
using Verse;

namespace RimClaw
{
    public class CompProperties_GPUCluster : CompProperties
    {
        public int providedVRAM = 300;
        public float heatPerSecond = 0.6f;

        public CompProperties_GPUCluster()
        {
            compClass = typeof(CompGPUCluster);
        }
    }

    public class CompGPUCluster : ThingComp
    {
        private int hostThingID = -1;

        public CompProperties_GPUCluster Props => (CompProperties_GPUCluster)props;
        public int HostThingID => hostThingID;

        public void AssignHost(Thing host)
        {
            hostThingID = host?.thingIDNumber ?? -1;
        }

        public void ClearHostIfMatches(Thing host)
        {
            if (hostThingID == (host?.thingIDNumber ?? -2))
            {
                hostThingID = -1;
            }
        }

        public bool IsAssignedTo(Thing host)
        {
            return hostThingID >= 0 && hostThingID == host.thingIDNumber;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hostThingID, "hostThingID", -1);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || !parent.IsHashIntervalTick(60))
            {
                return;
            }

            if (hostThingID < 0)
            {
                return;
            }

            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return;
            }

            GenTemperature.PushHeat(parent.Position, parent.Map, Props.heatPerSecond);
        }

        public override string CompInspectStringExtra()
        {
            return hostThingID < 0 ? "Host: unassigned" : $"Host ID: {hostThingID}\nVRAM: {Props.providedVRAM} GB";
        }
    }
}
