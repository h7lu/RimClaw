using RimWorld;
using Verse;

namespace RimClaw
{
    public class CompProperties_GPUCluster : CompProperties
    {
        public int providedVRAM = 150;
        public float baseHeatPerSecond = 5f;
        public float heatPerUsageFraction = 20f;
        public float heatAverageSeconds = 15f;
        public float maxTemperatureC = 1000f;
        public string inspectUnassigned = "Host: unassigned";
        public string inspectAssigned = "Host ID: {0}\nVRAM: {1} GB";

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

            // Heat is now pushed by the host computer service so the panel and runtime use the same smoothed value.
        }

        public override string CompInspectStringExtra()
        {
            return hostThingID < 0
                ? Props.inspectUnassigned
                : string.Format(Props.inspectAssigned, hostThingID, Props.providedVRAM);
        }
    }
}
