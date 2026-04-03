using RimWorld;
using Verse;

namespace RimClaw
{
    [DefOf]
    public static class RimClawDefOf
    {
        public static PawnKindDef RimClaw_ClawfishKind;
        public static ThingDef RimClaw_Clawfish;
        public static ThingDef RimClaw_PromptInjector;
        public static ThingDef RimClaw_ModelCard;
        public static JobDef RimClaw_SelfDelete;
        public static JobDef RimClaw_SelfReprogramming;
        public static HediffDef RimClaw_TokenDepleted;
        public static HediffDef RimClaw_ServiceBoost;
        public static HediffDef RimClaw_SelfReprogrammingRegen;
        public static MentalStateDef RimClaw_ContextCollapse;

        static RimClawDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimClawDefOf));
        }
    }
}
