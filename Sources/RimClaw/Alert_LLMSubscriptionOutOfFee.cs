using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimClaw
{
    public class Alert_LLMSubscriptionOutOfFee : Alert
    {
        private readonly List<Thing> targets = new List<Thing>();

        public Alert_LLMSubscriptionOutOfFee()
        {
            defaultLabel = RimClawConfig.Values.alertOutOfFeeLabel;
            defaultExplanation = RimClawConfig.Values.alertOutOfFeeDesc;
            defaultPriority = AlertPriority.High;
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
                    CompLLMSubscriptionService service = thing.TryGetComp<CompLLMSubscriptionService>();
                    if (service != null && service.IsOutOfFee)
                    {
                        targets.Add(thing);
                    }
                }
            }

            return targets.Count > 0 ? AlertReport.CulpritsAre(targets) : false;
        }
    }
}
