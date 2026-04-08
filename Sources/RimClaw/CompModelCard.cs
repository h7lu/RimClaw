using System.Collections.Generic;
using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_ModelCard : CompProperties
    {
        public List<string> namePrefixes = new List<string> { "kwen" };
        public List<string> nameSuffixes = new List<string> { "3.5_27b" };
        public int requiredVramMin = 20;
        public int requiredVramMax = 120;
        public float tokenPerSecondPerInstanceMin = 80f;
        public float tokenPerSecondPerInstanceMax = 260f;
        public float workSpeedBonusMin = 0.01f;
        public float workSpeedBonusMax = 0.06f;
        public float parameterSizeMinB = 0.1f;
        public float parameterSizeMaxB = 999f;

        public CompProperties_ModelCard()
        {
            compClass = typeof(CompModelCard);
        }
    }

    public class CompModelCard : ThingComp
    {
        private bool initialized;
        private string modelName;
        private string formalName;
        private string midName;
        private string parameterTag;
        private string sourceFactionId;
        private Color modelColor = Color.white;
        private float parameterSizeB;
        private float feeRate;
        private float workSpeedMultiplier;
        private int requiredVram;
        private float tokenPerSecondPerInstance;
        private float workSpeedBonus;
        private int modelSkillLevelAdjustment;

        public CompProperties_ModelCard Props => (CompProperties_ModelCard)props;

        public string ModelName => modelName;
        public string FormalName => formalName;
        public Color ModelColor => modelColor;
        public float FeeRate => feeRate;
        public float WorkSpeedMultiplier => workSpeedMultiplier;
        public float ParameterSizeB => parameterSizeB;
        public int RequiredVram => requiredVram;
        public float TokenPerSecondPerInstance => tokenPerSecondPerInstance;
        public float WorkSpeedBonus => workSpeedBonus;
        public int ModelSkillLevelAdjustment => modelSkillLevelAdjustment;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", defaultValue: false);
            Scribe_Values.Look(ref modelName, "modelName");
            Scribe_Values.Look(ref formalName, "formalName");
            Scribe_Values.Look(ref midName, "midName");
            Scribe_Values.Look(ref parameterTag, "parameterTag");
            Scribe_Values.Look(ref sourceFactionId, "sourceFactionId");
            Scribe_Values.Look(ref modelColor, "modelColor", Color.white);
            Scribe_Values.Look(ref parameterSizeB, "parameterSizeB", 0f);
            Scribe_Values.Look(ref feeRate, "feeRate", 1f);
            Scribe_Values.Look(ref workSpeedMultiplier, "workSpeedMultiplier", 1f);
            Scribe_Values.Look(ref requiredVram, "requiredVram", 40);
            Scribe_Values.Look(ref tokenPerSecondPerInstance, "tokenPerSecondPerInstance", 200f);
            Scribe_Values.Look(ref workSpeedBonus, "workSpeedBonus", 0.02f);
            Scribe_Values.Look(ref modelSkillLevelAdjustment, "modelSkillLevelAdjustment", 0);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureInitialized();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            RimClawModelWorldState worldState = RimClawModelWorldState.Instance;
            FactionModelProfile profile = worldState?.GetProfileForThing(parent) ?? worldState?.GetRandomFactionProfile();

            if (profile == null)
            {
                formalName = "Clode";
                sourceFactionId = "Fallback";
                modelColor = Color.white;
                feeRate = 1f;
                workSpeedMultiplier = 1f;
                parameterSizeB = Rand.Range(Props.parameterSizeMinB, Props.parameterSizeMaxB);
                midName = "Lite";
                parameterTag = FormatParameterTag(parameterSizeB);
                modelName = $"{formalName}-{midName}-{parameterTag}";
                requiredVram = 40;
                tokenPerSecondPerInstance = 100f;
                workSpeedBonus = 0.02f;
                modelSkillLevelAdjustment = 0;
                return;
            }

            sourceFactionId = profile.factionId;
            formalName = profile.formalName;
            modelColor = profile.modelColor;

            float tagTokenMultiplier;
            float tagVramMultiplier;
            midName = worldState.GenerateMidName(profile, out tagTokenMultiplier, out tagVramMultiplier);

            parameterSizeB = Rand.Range(Props.parameterSizeMinB, Props.parameterSizeMaxB);
            parameterTag = FormatParameterTag(parameterSizeB);
            modelName = $"{formalName}-{midName}-{parameterTag}";

            float logx = Mathf.Log10(Mathf.Max(0.1f, parameterSizeB));
            float baseFeeRate = 0.28f * (2.2f + logx);
            float baseWorkMultiplier = 0.6f + 0.3f * logx;
            float baseVram = 0.175f * parameterSizeB + 1.5f;
            float baseTokens = -0.15f * parameterSizeB + 500f;

            feeRate = Mathf.Max(0.1f, baseFeeRate * profile.feeRateMultiplier);
            workSpeedMultiplier = baseWorkMultiplier * profile.workSpeedMultiplier;
            requiredVram = Mathf.Max(1, Mathf.RoundToInt(baseVram * tagVramMultiplier));
            tokenPerSecondPerInstance = Mathf.Max(0.5f, baseTokens * tagTokenMultiplier);
            workSpeedBonus = workSpeedMultiplier - 1f;
            modelSkillLevelAdjustment = ComputeModelSkillAdjustment(profile);
        }

        public override Color? ForceColor()
        {
            EnsureInitialized();
            return modelColor;
        }

        public void OverrideModelData(string name, Color color, int vram, float tokenRate, float speedBonus, int skillAdjustment = 0)
        {
            initialized = true;
            modelName = name;
            formalName = name;
            midName = "Loaded";
            parameterTag = "custom";
            sourceFactionId = "Loaded";
            modelColor = color;
            requiredVram = Mathf.Max(1, vram);
            tokenPerSecondPerInstance = Mathf.Max(0.1f, tokenRate);
            workSpeedBonus = speedBonus;
            workSpeedMultiplier = Mathf.Max(0.1f, 1f + speedBonus);
            modelSkillLevelAdjustment = Mathf.Clamp(skillAdjustment, -4, 6);
            parameterSizeB = Mathf.Max(0.1f, requiredVram * 0.5f);
            feeRate = 1f;
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();
            return $"Model: {modelName}\nParameter size: {parameterSizeB:0.0}B\nRequired VRAM: {requiredVram} GB\nToken/s per instance: {tokenPerSecondPerInstance:0.0}\nFee rate: x{feeRate:0.00}\nWork speed: x{workSpeedMultiplier:0.00}\nSkill level adjust: {(modelSkillLevelAdjustment >= 0 ? "+" : string.Empty)}{modelSkillLevelAdjustment}";
        }

        private int ComputeModelSkillAdjustment(FactionModelProfile profile)
        {
            float formalFactor = GetFormalFactor(profile, formalName);
            float middleFactor = GetMiddleFactor(profile, midName);

            float logMin = Mathf.Log10(Mathf.Max(0.1f, Props.parameterSizeMinB));
            float logMax = Mathf.Log10(Mathf.Max(0.1f, Props.parameterSizeMaxB));
            float logSize = Mathf.Log10(Mathf.Max(0.1f, parameterSizeB));
            float sizeFactor = Mathf.Clamp01(Mathf.InverseLerp(logMin, logMax, logSize));

            // Multiplicative final stage over normalized [0,1] factors.
            // Raw product skews too low in practice, so use a power-lift to improve spread.
            float product = Mathf.Clamp01(formalFactor) * Mathf.Clamp01(middleFactor) * Mathf.Clamp01(sizeFactor);
            float combined = Mathf.Clamp01(Mathf.Pow(product, 0.5f));
            float adjustment = -4f + 10f * combined;
            return Mathf.Clamp(Mathf.RoundToInt(adjustment), -4, 6);
        }

        private static float GetFormalFactor(FactionModelProfile profile, string currentFormalName)
        {
            if (profile != null)
            {
                return NormalizeHalfToTwo(profile.workSpeedMultiplier);
            }

            if (string.IsNullOrEmpty(currentFormalName))
            {
                return 0.5f;
            }

            string[] formalPool = RimClawConfig.Values.modelFormalNamePool?.ToArray() ?? Array.Empty<string>();
            if (formalPool.Length > 1)
            {
                for (int i = 0; i < formalPool.Length; i++)
                {
                    if (string.Equals(formalPool[i], currentFormalName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Mathf.Clamp01((float)i / (formalPool.Length - 1));
                    }
                }
            }

            uint hash = (uint)GenText.StableStringHash(currentFormalName);
            return Mathf.Clamp01((hash % 1001u) / 1000f);
        }

        private static float GetMiddleFactor(FactionModelProfile profile, string middleName)
        {
            if (string.IsNullOrEmpty(middleName))
            {
                return 0.5f;
            }

            List<float> factors = new List<float>();

            // Numeric middle fragment: maps 1.0..9.9 to multiplier 0.5..2.0, then normalized to [0,1].
            if (TryExtractNumericFragment(middleName, out float numericPart))
            {
                float numericMul = Mathf.Clamp(0.5f + ((numericPart - 1f) / 8.9f) * 1.5f, 0.5f, 2f);
                factors.Add(NormalizeHalfToTwo(numericMul));
            }

            // Word middle fragment uses generated tag multipliers when available.
            string wordPart = ExtractKnownMidWord(middleName);
            if (!string.IsNullOrEmpty(wordPart))
            {
                if (profile != null)
                {
                    TagModelStats stats = profile.GetTag(wordPart) ?? profile.GetTag("None");
                    if (stats != null)
                    {
                        float avgMul = (stats.tokenPerSecondMultiplier + stats.vramMultiplier) * 0.5f;
                        factors.Add(NormalizeHalfToTwo(avgMul));
                    }
                }
                else
                {
                    string[] words = RimClawConfig.Values.modelMidWordPool?.ToArray() ?? Array.Empty<string>();
                    if (words.Length > 1)
                    {
                        for (int i = 0; i < words.Length; i++)
                        {
                            if (string.Equals(words[i], wordPart, StringComparison.OrdinalIgnoreCase))
                            {
                                factors.Add(Mathf.Clamp01((float)i / (words.Length - 1)));
                                break;
                            }
                        }
                    }
                }
            }

            if (factors.Count == 0)
            {
                return 0.5f;
            }

            float sum = 0f;
            for (int i = 0; i < factors.Count; i++)
            {
                sum += Mathf.Clamp01(factors[i]);
            }

            return Mathf.Clamp01(sum / factors.Count);
        }

        private static float NormalizeHalfToTwo(float value)
        {
            return Mathf.Clamp01((Mathf.Clamp(value, 0.5f, 2f) - 0.5f) / 1.5f);
        }

        private static bool TryExtractNumericFragment(string middleName, out float numeric)
        {
            numeric = 0f;
            if (string.IsNullOrEmpty(middleName))
            {
                return false;
            }

            for (int i = 0; i < middleName.Length; i++)
            {
                if (!(char.IsDigit(middleName[i]) || middleName[i] == '.'))
                {
                    continue;
                }

                int start = i;
                while (i < middleName.Length && (char.IsDigit(middleName[i]) || middleName[i] == '.'))
                {
                    i++;
                }

                string token = middleName.Substring(start, i - start);
                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    numeric = Mathf.Clamp(parsed, 1f, 9.9f);
                    return true;
                }
            }

            return false;
        }

        private static string ExtractKnownMidWord(string middleName)
        {
            if (string.IsNullOrEmpty(middleName))
            {
                return null;
            }

            string[] words = RimClawConfig.Values.modelMidWordPool?.ToArray() ?? Array.Empty<string>();
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (!string.IsNullOrEmpty(word) && middleName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return word;
                }
            }

            return null;
        }

        private static string FormatParameterTag(float sizeB)
        {
            if (sizeB >= 10f)
            {
                return $"{Mathf.RoundToInt(sizeB)}b";
            }

            return $"{sizeB:0.0}b";
        }
    }
}
