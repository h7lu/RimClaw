using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimClaw
{
    public class Alert_GPUClusterBreakRisk : Alert
    {
        private readonly List<Thing> targets = new List<Thing>();

        public Alert_GPUClusterBreakRisk()
        {
            defaultLabel = RimClawConfig.Values.alertGpuBreakRiskLabel;
            defaultExplanation = RimClawConfig.Values.alertGpuBreakRiskDesc;
            defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            targets.Clear();

            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (!GpuBreakdownRiskUtility.IsGpuThing(thing))
                    {
                        continue;
                    }

                    CompBreakdownable breakdownable = thing.TryGetComp<CompBreakdownable>();
                    if (!GpuBreakdownRiskUtility.CanBreakdownNow(breakdownable))
                    {
                        continue;
                    }

                    float chancePerSecond = GpuBreakdownRiskUtility.ComputeBreakChancePerSecond(thing);
                    if (chancePerSecond > GpuBreakdownRiskUtility.WarningThresholdPerSecond)
                    {
                        targets.Add(thing);
                    }
                }
            }

            return targets.Count > 0 ? AlertReport.CulpritsAre(targets) : false;
        }
    }
}