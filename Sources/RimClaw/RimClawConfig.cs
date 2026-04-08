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
        public string promptInjectorSuccessLetterLabel = "Prompt Injection Success";
        public string promptInjectorSuccessLetterText = "{0} has been successfully converted and joined your colony as a clawfish LLM Agent.";
        public string allClawColonyLetterLabel = "All Claw Colony";
        public string allClawColonyLetterTextChanged = "Your colony now consists only of clawfish colonists.\n\nMechanoid Hive diplomatic status updated:\n- Relation set to Ally (+100 goodwill)\n- Natural goodwill baseline set to +20\n- Faction visibility enabled";
        public string allClawColonyLetterTextUnchanged = "Your colony now consists only of clawfish colonists.\n\nMechanoid Hive is already allied (+100), natural goodwill baseline is +20, and visibility is enabled.";

        public float promptInjectorStunChance = RimClawSettings.DefaultPromptInjectorStunChance;
        public int promptInjectorStunTicks = RimClawSettings.DefaultPromptInjectorStunTicks;

        public float promptInjectorJoinChance = RimClawSettings.DefaultPromptInjectorJoinChance;
        public float promptInjectorBerserkChance = RimClawSettings.DefaultPromptInjectorBerserkChance;
        public float promptInjectorSelfDeleteChance = RimClawSettings.DefaultPromptInjectorSelfDeleteChance;

        public int selfDeleteTicks = RimClawSettings.DefaultSelfDeleteTicks;
        public int selfReprogrammingTicks = 1200;
        public float selfRegenHpPerDay = 300f;

        public int contextCollapseStartTicks = 10000;
        public int contextCollapseRollStartTicks = 12500;
        public float contextCollapseChancePerSecond = 0.001f;
        public int contextCollapseDurationTicks = 5000;

        public int clawfishSkillLevel = RimClawSettings.DefaultClawfishSkillLevel;
        public int nameMinPid = RimClawSettings.DefaultNameMinPid;
        public int nameMaxPid = RimClawSettings.DefaultNameMaxPid;
        public List<string> nameFragments = new List<string>(RimClawSettings.DefaultNameFragments);
        public List<string> modelFormalNamePool = new List<string>
        {
            "Clode", "Astra", "Nexa", "Orin", "Vanta", "Helix", "Quanta", "Myria", "Luma", "Cinder",
            "Arcus", "Radian", "Solace", "Kestrel", "Nova", "Caelum", "Iris", "Verdan", "Talon", "Echo"
        };
        public List<string> modelMidWordPool = new List<string>
        {
            "Flash", "Rapid", "Fast", "Instruct", "Lite", "Linear", "PagedAttn", "Mini", "Thinking", "Deepthink", "Pro", "Max", "Omni"
        };
        public List<string> modelTpsOrderPool = new List<string>
        {
            "Flash", "Rapid", "Fast", "Instruct", "Lite", "Linear", "PagedAttn", "Mini", "None", "Thinking", "Deepthink", "Pro", "Max", "Omni"
        };
        public List<string> modelVramOrderPool = new List<string>
        {
            "Omni", "Max", "Pro", "Deepthink", "Thinking", "None", "Instruct", "Rapid", "PagedAttn", "Fast", "Flash", "Mini", "Lite", "Linear"
        };

        public string convertedPawnKindDefName = "RimClaw_ClawfishColonist";
        public List<string> allowedSurgeryDefNames = new List<string>();
        public List<string> allowedSurgeryNameContains = new List<string> { "Mechanitor", "Skill" };

        public string clawfishWildTexPath = "clawfish";
        public string clawfishTamedTexPath = "clawfish_tamed";

        public int clawfishColorR1 = 130;
        public int clawfishColorG1 = 60;
        public int clawfishColorB1 = 80;
        public int clawfishColorR2 = 200;
        public int clawfishColorG2 = 100;
        public int clawfishColorB2 = 0;
        public float clawfishColorDeviationRadius = 30f;
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
