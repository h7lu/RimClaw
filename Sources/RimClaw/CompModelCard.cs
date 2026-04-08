using System.Collections.Generic;
using System;
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
            float baseWorkMultiplier = Mathf.Max(0.1f, 0.9f + logx);
            float baseVram = 0.35f * parameterSizeB + 3f;
            float baseTokens = -0.015f * parameterSizeB + 36f;

            feeRate = Mathf.Max(0.1f, baseFeeRate * profile.feeRateMultiplier);
            workSpeedMultiplier = Mathf.Max(0.1f, baseWorkMultiplier * profile.workSpeedMultiplier);
            requiredVram = Mathf.Max(1, Mathf.RoundToInt(baseVram * tagVramMultiplier));
            tokenPerSecondPerInstance = Mathf.Max(0.5f, baseTokens * tagTokenMultiplier);
            workSpeedBonus = workSpeedMultiplier - 1f;
        }

        public override Color? ForceColor()
        {
            EnsureInitialized();
            return modelColor;
        }

        public void OverrideModelData(string name, Color color, int vram, float tokenRate, float speedBonus)
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
            parameterSizeB = Mathf.Max(0.1f, requiredVram * 0.5f);
            feeRate = 1f;
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();
            return $"Model: {modelName}\nParameter size: {parameterSizeB:0.0}B\nRequired VRAM: {requiredVram} GB\nToken/s per instance: {tokenPerSecondPerInstance:0.0}\nFee rate: x{feeRate:0.00}\nWork speed: x{workSpeedMultiplier:0.00}";
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
