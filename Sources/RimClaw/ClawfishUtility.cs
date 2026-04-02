using System;
using RimWorld;
using Verse;

namespace RimClaw
{
    public static class ClawfishUtility
    {
        private static readonly string[] NameFragments =
        {
            "Auto", "Vector", "Kernel", "Delta", "Omega", "Cloud", "Prompt", "Runtime", "Signal", "Code"
        };

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
                    skill.Level = 8;
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
            string frag = NameFragments[Rand.Range(0, NameFragments.Length)];
            int pid = Rand.RangeInclusive(10000, 999999);
            return $"{frag}Claw pid={pid}";
        }
    }
}
