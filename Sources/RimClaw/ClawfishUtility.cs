using RimWorld;
using Verse;
using UnityEngine;

namespace RimClaw
{
    public static class ClawfishUtility
    {
        public static bool IsClawfish(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.def == RimClawDefOf.RimClaw_Clawfish || pawn.def == RimClawDefOf.RimClaw_ClawfishHuman)
            {
                return true;
            }

            return pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_DisguisedClawfish) != null;
        }

        public static bool IsClawfishColonist(Pawn pawn)
        {
            return pawn?.def == RimClawDefOf.RimClaw_ClawfishHuman;
        }

        public static void ApplyBaseline(Pawn pawn)
        {
            if (!IsClawfish(pawn))
            {
                return;
            }

            EnsureRandomColor(pawn);

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

        public static void EnsureServiceBoost(Pawn pawn, float bonus)
        {
            if (!IsClawfish(pawn) || pawn.health == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ServiceBoost);
            if (hediff == null)
            {
                hediff = pawn.health.AddHediff(RimClawDefOf.RimClaw_ServiceBoost);
            }

            hediff.Severity = bonus;
        }

        public static Graphic GetClawfishBodyGraphic(Pawn pawn)
        {
            if (!IsClawfish(pawn))
            {
                return null;
            }

            GraphicData graphicData = pawn.def.graphicData ?? RimClawDefOf.RimClaw_Clawfish?.graphicData;
            if (graphicData == null)
            {
                return null;
            }

            string path = GetTexturePathForPawn(pawn);
            Shader shader = graphicData.shaderType?.Shader ?? ShaderDatabase.CutoutComplex;
            Color primary = GetOrAssignColor(pawn);
            return GraphicDatabase.Get(typeof(Graphic_Multi_EastScaled), path, shader, graphicData.drawSize, primary, Color.white, graphicData, graphicData.shaderParameters, graphicData.maskPath);
        }

        public static Graphic GetDisguisedClawfishGraphic(Pawn pawn)
        {
            Color color;
            if (TryGetDisguisedClawfishColor(pawn, out color))
            {
                return GetClawfishGraphic(pawn, color);
            }

            if (!IsClawfish(pawn))
            {
                return null;
            }

            return GetClawfishGraphic(pawn, GetOrAssignColor(pawn));
        }

        private static Graphic GetClawfishGraphic(Pawn pawn, Color color)
        {
            GraphicData graphicData = RimClawDefOf.RimClaw_Clawfish?.graphicData ?? pawn?.def?.graphicData;
            if (graphicData == null)
            {
                return null;
            }

            return GraphicDatabase.Get(
                typeof(Graphic_Multi_EastScaled),
                GetTexturePathForPawn(pawn),
                graphicData.shaderType?.Shader ?? ShaderDatabase.CutoutComplex,
                graphicData.drawSize,
                color,
                Color.white,
                graphicData,
                graphicData.shaderParameters,
                graphicData.maskPath);
        }

        public static string GetPortraitSouthTexPath(Pawn pawn)
        {
            return GetTexturePathForPawn(pawn) + "_south";
        }

        private static string GetTexturePathForPawn(Pawn pawn)
        {
            if (pawn?.def == RimClawDefOf.RimClaw_ClawfishHuman)
            {
                return RimClawConfig.Values.clawfishTamedTexPath;
            }

            if (TryGetDisguisedClawfishColor(pawn, out _))
            {
                return RimClawConfig.Values.clawfishTamedTexPath;
            }

            return RimClawConfig.Values.clawfishWildTexPath;
        }

        public static bool TryGetDisguisedClawfishColor(Pawn pawn, out Color color)
        {
            color = Color.white;
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            Hediff_DisguisedClawfish hediff = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_DisguisedClawfish) as Hediff_DisguisedClawfish;
            if (hediff == null)
            {
                return false;
            }

            color = hediff.DisplayColor;
            return true;
        }

        public static Color GetOrAssignColor(Pawn pawn)
        {
            return EnsureRandomColor(pawn);
        }

        private static Color EnsureRandomColor(Pawn pawn)
        {
            CompClawfishColor comp = pawn?.TryGetComp<CompClawfishColor>();
            if (comp == null)
            {
                return Color.white;
            }

            if (!comp.Initialized)
            {
                comp.Initialize(GenerateRandomClawfishColor());
            }

            return comp.Color;
        }

        private static Color GenerateRandomClawfishColor()
        {
            var cfg = RimClawConfig.Values;
            Vector3 min = new Vector3(
                Mathf.Min(cfg.clawfishColorR1, cfg.clawfishColorR2),
                Mathf.Min(cfg.clawfishColorG1, cfg.clawfishColorG2),
                Mathf.Min(cfg.clawfishColorB1, cfg.clawfishColorB2));
            Vector3 max = new Vector3(
                Mathf.Max(cfg.clawfishColorR1, cfg.clawfishColorR2),
                Mathf.Max(cfg.clawfishColorG1, cfg.clawfishColorG2),
                Mathf.Max(cfg.clawfishColorB1, cfg.clawfishColorB2));

            Vector3 baseColor = new Vector3(
                Rand.Range(min.x, max.x),
                Rand.Range(min.y, max.y),
                Rand.Range(min.z, max.z));

            Vector3 deviated = baseColor + RandomInsideRgbSphere(Mathf.Max(0f, cfg.clawfishColorDeviationRadius));
            deviated.x = Mathf.Clamp(deviated.x, 0f, 255f);
            deviated.y = Mathf.Clamp(deviated.y, 0f, 255f);
            deviated.z = Mathf.Clamp(deviated.z, 0f, 255f);

            Color candidate = new Color(deviated.x / 255f, deviated.y / 255f, deviated.z / 255f, 1f);
            Color.RGBToHSV(candidate, out float h, out float s, out float v);
            s = Mathf.Max(s, 0.65f);
            v = Mathf.Min(v, 0.88f);
            return Color.HSVToRGB(h, s, v);
        }

        private static Vector3 RandomInsideRgbSphere(float radius)
        {
            float u = Rand.Value;
            float v = Rand.Value;
            float w = Rand.Value;

            float theta = 2f * Mathf.PI * u;
            float phi = Mathf.Acos(2f * v - 1f);
            float r = radius * Mathf.Pow(w, 1f / 3f);

            float sinPhi = Mathf.Sin(phi);
            return new Vector3(
                r * sinPhi * Mathf.Cos(theta),
                r * sinPhi * Mathf.Sin(theta),
                r * Mathf.Cos(phi));
        }

        public static void EnsureAiTraitForLlmAgent(Pawn pawn)
        {
            if (!IsClawfishColonist(pawn) || pawn?.story?.traits == null || RimClawDefOf.RimClaw_AI == null)
            {
                return;
            }

            if (pawn.story.Adulthood?.defName != "RimClaw_Adulthood_LLM_Agent")
            {
                return;
            }

            if (!pawn.story.traits.HasTrait(RimClawDefOf.RimClaw_AI))
            {
                pawn.story.traits.GainTrait(new Trait(RimClawDefOf.RimClaw_AI));
            }
        }

        public static bool HasAiMoodLock(Pawn pawn)
        {
            return IsClawfishColonist(pawn)
                && pawn?.story?.traits != null
                && RimClawDefOf.RimClaw_AI != null
                && pawn.story.traits.HasTrait(RimClawDefOf.RimClaw_AI);
        }

        public static void EnsureSkillFloor(Pawn pawn, int minLevel)
        {
            if (pawn?.skills == null)
            {
                return;
            }

            for (int i = 0; i < pawn.skills.skills.Count; i++)
            {
                SkillRecord skill = pawn.skills.skills[i];
                if (skill.Level < minLevel)
                {
                    skill.Level = minLevel;
                    skill.xpSinceLastLevel = 0f;
                }
            }
        }
    }
}
