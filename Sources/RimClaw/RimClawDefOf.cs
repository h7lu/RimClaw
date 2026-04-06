using RimWorld;
using Verse;

namespace RimClaw
{
    [DefOf]
    public static class RimClawDefOf
    {
        public static PawnKindDef RimClaw_ClawfishKind;
        public static PawnKindDef RimClaw_ClawfishColonist;
        public static ThingDef RimClaw_Clawfish;
        public static ThingDef RimClaw_ClawfishHuman;
        public static ThingDef RimClaw_PromptInjector;
        public static ThingDef RimClaw_ModelCard;
        public static JobDef RimClaw_SelfDelete;
        public static JobDef RimClaw_SelfReprogramming;
        public static HediffDef RimClaw_TokenDepleted;
        public static HediffDef RimClaw_ServiceBoost;
        public static HediffDef RimClaw_SelfReprogrammingRegen;
        public static HediffDef RimClaw_DisguisedClawfish;
        public static HediffDef RimClaw_InsufficientIORate;
        public static MentalStateDef RimClaw_ContextCollapse;
        public static TraitDef RimClaw_AI;

        static RimClawDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimClawDefOf));
        }
    }
}
