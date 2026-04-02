using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimClaw
{
    public class RimClawConfigExtension : DefModExtension
    {
        public float fishingSpawnChance = RimClawSettings.DefaultFishingSpawnChance;
        public int spawnRadiusFromFisher = RimClawSettings.DefaultSpawnRadiusFromFisher;

        public string fishingLetterLabel = "Clawfish Hooked";
        public string fishingLetterText = "{0} hooked a Clawfish and aborted fishing.";

        public float promptInjectorStunChance = RimClawSettings.DefaultPromptInjectorStunChance;
        public int promptInjectorStunTicks = RimClawSettings.DefaultPromptInjectorStunTicks;

        public float promptInjectorJoinChance = RimClawSettings.DefaultPromptInjectorJoinChance;
        public float promptInjectorBerserkChance = RimClawSettings.DefaultPromptInjectorBerserkChance;
        public float promptInjectorSelfDeleteChance = RimClawSettings.DefaultPromptInjectorSelfDeleteChance;

        public int selfDeleteTicks = RimClawSettings.DefaultSelfDeleteTicks;

        public int clawfishSkillLevel = RimClawSettings.DefaultClawfishSkillLevel;
        public int nameMinPid = RimClawSettings.DefaultNameMinPid;
        public int nameMaxPid = RimClawSettings.DefaultNameMaxPid;
        public List<string> nameFragments = new List<string>(RimClawSettings.DefaultNameFragments);

        public string convertedPawnKindDefName = "Colonist";
    }

    public static class RimClawConfig
    {
        private static RimClawConfigExtension cached;

        public static RimClawConfigExtension Values
        {
            get
            {
                if (cached == null)
                {
                    ThingDef clawfishDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimClaw_Clawfish");
                    cached = clawfishDef?.GetModExtension<RimClawConfigExtension>() ?? new RimClawConfigExtension();
                }

                return cached;
            }
        }

        public static PawnKindDef ConvertedPawnKind
        {
            get
            {
                PawnKindDef def = DefDatabase<PawnKindDef>.GetNamedSilentFail(Values.convertedPawnKindDefName);
                return def ?? PawnKindDefOf.Colonist;
            }
        }
    }
}
