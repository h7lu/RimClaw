using System.Collections.Generic;
using Verse;

namespace RimClaw
{
    public class RoomRoleWorker_Datacenter : RoomRoleWorker
    {
        private const float ScorePerHostComputer = 1000f;

        public override float GetScore(Room room)
        {
            if (room == null)
            {
                return 0f;
            }

            int hostCount = 0;
            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].TryGetComp<CompHostComputerService>() != null)
                {
                    hostCount++;
                }
            }

            return hostCount * ScorePerHostComputer;
        }

        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef buildingDef)
        {
            if (buildingDef?.comps == null)
            {
                return 0f;
            }

            for (int i = 0; i < buildingDef.comps.Count; i++)
            {
                if (buildingDef.comps[i]?.compClass == typeof(CompHostComputerService))
                {
                    return ScorePerHostComputer;
                }
            }

            return 0f;
        }
    }
}