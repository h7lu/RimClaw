using RimWorld;
using Verse;

namespace RimClaw
{
    public static class ClawfishUtility
    {
        public static bool IsClawfish(Pawn pawn)
        {
            return pawn?.def == RimClawDefOf.RimClaw_Clawfish;
        }

        public static void ApplyBaseline(Pawn pawn)
        {
            if (!IsClawfish(pawn))
            {
                return;
            }

            if (pawn.skills != null)
            {
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    skill.Level = RimClawConfig.Values.clawfishSkillLevel;
                    skill.xpSinceLastLevel = 0f;
                    skill.passion = Passion.None;
                }
            }

            if (pawn.Name == null || pawn.Name is NameSingle)
            {
                pawn.Name = new NameSingle(GenerateName());
            }
        }

        public static string GenerateName()
        {
            var cfg = RimClawConfig.Values;
            var fragments = cfg.nameFragments;
            if (fragments == null || fragments.Count == 0)
            {
                fragments = new System.Collections.Generic.List<string>(RimClawSettings.DefaultNameFragments);
            }

            string frag = fragments[Rand.Range(0, fragments.Count)];
            int minPid = System.Math.Min(cfg.nameMinPid, cfg.nameMaxPid);
            int maxPid = System.Math.Max(cfg.nameMinPid, cfg.nameMaxPid);
            int pid = Rand.RangeInclusive(minPid, maxPid);
            return $"{frag}Claw pid={pid}";
        }
    }
}
