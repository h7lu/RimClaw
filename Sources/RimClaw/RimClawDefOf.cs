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
        public static JobDef RimClaw_SelfDelete;

        static RimClawDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimClawDefOf));
        }
    }
}
