using RimWorld;
using Verse;

namespace RimClaw
{
    public class PlaceWorker_NotUnderThickRoof : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            RoofDef roof = loc.GetRoof(map);
            if (roof != null && roof.isThickRoof)
            {
                return "CannotPlaceInThickRoof";
            }

            return true;
        }
    }
}
