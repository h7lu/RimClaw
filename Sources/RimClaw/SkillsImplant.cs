using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public enum SkillsEffectKind
    {
        SkillBoost,
        AdditiveStatBoost,
        ReductionMultiplier,
        PromptInjectionResistance
    }

    public class SkillsEffectRecord : IExposable
    {
        public SkillsEffectKind Kind;
        public string TargetDefName;
        public int LevelShift;
        public float Multiplier = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind, "kind", SkillsEffectKind.SkillBoost);
            Scribe_Values.Look(ref TargetDefName, "targetDefName");
            Scribe_Values.Look(ref LevelShift, "levelShift", 0);
            Scribe_Values.Look(ref Multiplier, "multiplier", 1f);
        }

        public string Describe()
        {
            switch (Kind)
            {
                case SkillsEffectKind.SkillBoost:
                    return $"{ResolveSkillLabel(TargetDefName)} {(LevelShift >= 0 ? "+" : string.Empty)}{LevelShift}";
                case SkillsEffectKind.AdditiveStatBoost:
                    float percentPerPoint = SkillsImplantUtility.GetAdditivePercentPerPoint(TargetDefName);
                    return $"{ResolveAdditiveStatLabel(TargetDefName)} {(LevelShift >= 0 ? "+" : "-")}{Mathf.Abs(LevelShift * percentPerPoint * 100f):0.#}%";
                case SkillsEffectKind.ReductionMultiplier:
                    return $"{ResolveReductionLabel(TargetDefName)} x{Multiplier:0.00}";
                case SkillsEffectKind.PromptInjectionResistance:
                    return "RimClaw_Skills_PromptInjectionChance".Translate(Multiplier.ToString("0.00"));
                default:
                    return TargetDefName ?? Kind.ToString();
            }
        }

        private static string ResolveSkillLabel(string defName)
        {
            SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return def?.label?.CapitalizeFirst() ?? defName ?? "RimClaw_Skills_FallbackSkill".Translate().ToString();
        }

        private static string ResolveAdditiveStatLabel(string defName)
        {
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(defName);
            return stat?.LabelCap ?? defName ?? "RimClaw_Skills_FallbackStat".Translate().ToString();
        }

        private static string ResolveReductionLabel(string defName)
        {
            switch (defName)
            {
                case "ContextCollapseChance":
                    return "RimClaw_Skills_ContextCorruptionChance".Translate();
                default:
                    return defName ?? "RimClaw_Skills_FallbackRate".Translate().ToString();
            }
        }
    }

    public class CompProperties_SkillsMd : CompProperties
    {
        public List<string> namePrefixes = new List<string>
        {
            "cipher",
            "vector",
            "atlas",
            "kernel",
            "signal",
            "index",
            "syntax",
            "node"
        };

        public List<string> nameSuffixes = new List<string>
        {
            "core",
            "stack",
            "prime",
            "node",
            "grid",
            "line",
            "mirror",
            "script"
        };

        public int minEffects = 1;
        public int maxEffects = 4;
        public float levelPercent = 0.03f;

        public CompProperties_SkillsMd()
        {
            compClass = typeof(CompSkillsMd);
        }
    }

    public class CompSkillsMd : ThingComp
    {
        private bool initialized;
        private string codename;
        private QualityCategory generatedQuality = QualityCategory.Normal;
        private List<SkillsEffectRecord> effects = new List<SkillsEffectRecord>();

        public CompProperties_SkillsMd Props => (CompProperties_SkillsMd)props;
        public string Codename => codename;
        public QualityCategory CurrentQuality => GetQualityCategory();
        public int QualityShift => GetQualityShift(CurrentQuality);
        public string QualityLabel => CurrentQuality.GetLabel();
        public List<SkillsEffectRecord> Effects => effects;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref codename, "codename");
            Scribe_Values.Look(ref generatedQuality, "generatedQuality", QualityCategory.Normal);
            Scribe_Collections.Look(ref effects, "effects", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && effects == null)
            {
                effects = new List<SkillsEffectRecord>();
            }

            EnsureInitialized();

            NormalizeEffects();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureInitialized();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureInitialized();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "RimClaw_Skills_Install_Label".Translate(),
                defaultDesc = "RimClaw_Skills_Install_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("install_skill_md", reportFailure: false),
                action = BeginInstallTargeting
            };
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();

            return "RimClaw_Skills_Inspect".Translate(codename, BuildEffectsText());
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            EnsureInitialized();

            yield return new StatDrawEntry(
                StatCategoryDefOf.Basics,
                "RimClaw_Skills_Name_Label".Translate(),
                codename ?? "RimClaw_Generic_Unnamed".Translate().ToString(),
                "RimClaw_Skills_Name_Desc".Translate(),
                2000);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Basics,
                "RimClaw_Skills_Effects_Label".Translate(),
                BuildEffectsText(),
                "RimClaw_Skills_Effects_Desc".Translate(),
                1999);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent?.Spawned != true)
            {
                return;
            }

            if (!effects.NullOrEmpty())
            {
                RimClawGlowUtility.SpawnPulseGlow(parent, new Color(0.75f, 0.92f, 1f, 1f), 2f);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized && generatedQuality == CurrentQuality)
            {
                return;
            }

            initialized = true;
            GenerateSkillsMd();
        }

        private void GenerateSkillsMd()
        {
            CompProperties_SkillsMd props = Props;
            string prefix = props.namePrefixes.Count > 0 ? props.namePrefixes.RandomElement() : "cipher";
            string suffix = props.nameSuffixes.Count > 0 ? props.nameSuffixes.RandomElement() : "core";
            codename = $"{prefix}-{suffix}.md";

            GenerateEffectsFromQualityShift(QualityShift, CurrentQuality);
            generatedQuality = CurrentQuality;
            NormalizeEffects();
        }

        private QualityCategory GetQualityCategory()
        {
            CompQuality qualityComp = parent?.TryGetComp<CompQuality>();
            if (qualityComp != null)
            {
                return qualityComp.Quality;
            }

            return QualityCategory.Normal;
        }

        public static int GetQualityShift(QualityCategory quality)
        {
            switch (quality)
            {
                case QualityCategory.Awful:
                    return -1;
                case QualityCategory.Poor:
                    return 1;
                case QualityCategory.Normal:
                    return 3;
                case QualityCategory.Good:
                    return 5;
                case QualityCategory.Excellent:
                    return 7;
                case QualityCategory.Masterwork:
                    return 9;
                case QualityCategory.Legendary:
                    return 12;
                default:
                    return 3;
            }
        }

        private void GenerateEffectsFromQualityShift(int qualityShift, QualityCategory quality)
        {
            effects.Clear();
            
            // qualityShift is the total point budget for effects
            if (qualityShift == 0)
            {
                effects.Add(CreateEffect(SkillsEffectKind.SkillBoost, 1));
                return;
            }

            bool allowNegativeSkill = quality != QualityCategory.Masterwork && quality != QualityCategory.Legendary;
            
            // Generate effects that sum exactly to qualityShift
            List<EffectAllocation> effectsToCreate = DistributePoints(qualityShift, allowNegativeSkill);
            
            foreach (var alloc in effectsToCreate)
            {
                effects.Add(CreateEffect(alloc.Kind, alloc.Points));
            }
        }

        private struct EffectAllocation
        {
            public SkillsEffectKind Kind;
            public int Points;
        }

        private List<EffectAllocation> DistributePoints(int totalPoints, bool allowNegativeSkill)
        {
            List<EffectAllocation> result = new List<EffectAllocation>();
            int remaining = totalPoints;

            // First, always add at least one skill boost effect with remaining points if all used up
            List<SkillsEffectKind> availableKinds = new List<SkillsEffectKind>
            {
                SkillsEffectKind.SkillBoost,
                SkillsEffectKind.AdditiveStatBoost,
                SkillsEffectKind.ReductionMultiplier,
                SkillsEffectKind.PromptInjectionResistance
            };

            // Allocate up to 4 effects
            int effectCount = Mathf.Min(4, Mathf.Max(1, Mathf.Abs(totalPoints)));
            effectCount = Mathf.Min(effectCount, Props.maxEffects);
            
            // Distribute points among effects
            while (remaining != 0 && result.Count < effectCount)
            {
                int slotsLeft = effectCount - result.Count;
                int pointsForThisEffect;

                if (slotsLeft == 1)
                {
                    // Last effect gets all remaining points
                    pointsForThisEffect = remaining;
                }
                else
                {
                    // Distribute somewhat evenly, but allow variance
                    int avgPerEffect = remaining / slotsLeft;
                    int maxForThis = Mathf.Max(1, Mathf.Abs(avgPerEffect) + Mathf.Abs(avgPerEffect));
                    int absRemaining = Mathf.Abs(remaining);
                    pointsForThisEffect = Rand.RangeInclusive(
                        Mathf.Min(1, absRemaining),
                        Mathf.Min(maxForThis, absRemaining)
                    );
                    
                    // Preserve sign
                    if (remaining < 0)
                    {
                        pointsForThisEffect = -pointsForThisEffect;
                    }
                }

                SkillsEffectKind kind = availableKinds.RandomElement();
                
                // Ensure skill boosts respect constraints
                if (kind == SkillsEffectKind.SkillBoost && !allowNegativeSkill && pointsForThisEffect < 0)
                {
                    pointsForThisEffect = Mathf.Abs(pointsForThisEffect);
                }

                result.Add(new EffectAllocation { Kind = kind, Points = pointsForThisEffect });
                remaining -= pointsForThisEffect;
            }

            // Handle any remaining points due to rounding
            if (remaining != 0 && result.Count > 0)
            {
                var last = result[result.Count - 1];
                last.Points += remaining;
                result[result.Count - 1] = last;
            }

            return result;
        }

        private string BuildEffectsText()
        {
            if (effects.Count == 0)
            {
                return "RimClaw_Generic_None".Translate();
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                lines.Add(effects[i].Describe());
            }

            return string.Join("\n", lines);
        }

        private void NormalizeEffects()
        {
            if (effects == null || effects.Count <= 1)
            {
                return;
            }

            List<SkillsEffectRecord> normalized = new List<SkillsEffectRecord>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillsEffectRecord effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                SkillsEffectRecord existing = null;
                for (int j = 0; j < normalized.Count; j++)
                {
                    if (normalized[j].Kind == effect.Kind && normalized[j].TargetDefName == effect.TargetDefName)
                    {
                        existing = normalized[j];
                        break;
                    }
                }

                if (existing == null)
                {
                    normalized.Add(effect);
                    continue;
                }

                existing.LevelShift += effect.LevelShift;
                if (existing.Kind == SkillsEffectKind.ReductionMultiplier || existing.Kind == SkillsEffectKind.PromptInjectionResistance)
                {
                    existing.Multiplier = Mathf.Clamp(1f - existing.LevelShift * Props.levelPercent, 0.2f, 2f);
                }
            }

            effects = normalized;
        }

        private SkillsEffectRecord CreateEffect(SkillsEffectKind kind, int amount)
        {
            SkillsEffectRecord effect = new SkillsEffectRecord
            {
                Kind = kind,
                LevelShift = amount == 0 ? 1 : amount
            };

            switch (kind)
            {
                case SkillsEffectKind.SkillBoost:
                    effect.TargetDefName = ResolveRandomSkillDefName();
                    break;
                case SkillsEffectKind.AdditiveStatBoost:
                    effect.TargetDefName = ResolveRandomAdditiveStatDefName(effect.LevelShift >= 0);
                    break;
                case SkillsEffectKind.ReductionMultiplier:
                    effect.TargetDefName = ResolveRandomReductionTarget();
                    // For reduction: +1 level = x0.97 (3% reduction), -1 level = x1.03 (3% increase)
                    // Multiplier = 1 - (levelShift * 0.03)
                    // Will be clamped to [0.2, 2.0] during application
                    effect.Multiplier = 1f - effect.LevelShift * Props.levelPercent;
                    break;
                case SkillsEffectKind.PromptInjectionResistance:
                    effect.TargetDefName = "PromptInjectionResistance";
                    // Same as ReductionMultiplier: +1 = x0.97, -1 = x1.03
                    effect.Multiplier = 1f - effect.LevelShift * Props.levelPercent;
                    break;
            }

            return effect;
        }

        private static string ResolveRandomSkillDefName()
        {
            List<SkillDef> skills = DefDatabase<SkillDef>.AllDefsListForReading;
            if (skills == null || skills.Count == 0)
            {
                return "Melee";
            }

            return skills.RandomElement().defName;
        }

        private static string ResolveRandomAdditiveStatDefName(bool positiveShift)
        {
            List<string> legacyThreePercentStats = new List<string>
            {
                "MoveSpeed",
                "WorkSpeedGlobal",
                "SocialImpact",
                "NegotiationAbility"
            };

            List<string> tenPercentPositiveStats = new List<string>
            {
                "MiningYield",
                "DeepDrillingSpeed",
                "MiningSpeed",
                "MedicalSurgerySuccessChance",
                "MedicalOperationSpeed",
                "MedicalTendQuality",
                "MedicalTendSpeed",
                "ConstructionSpeed",
                "ConstructSuccessChance",
                "ReadingSpeed",
                "RepairSuccessChance",
                "DrugHarvestYield",
                "PlantHarvestYield",
                "PlantWorkSpeed",
                "CookingSpeed",
                "ButcherySpeed",
                "ButcheryEfficiency",
                "MechanoidShreddingSpeed",
                "MechanoidShreddingEfficiency",
                "DrugSynthesisSpeed",
                "DrugCookingSpeed",
                "HackingSpeed",
                "HackingStealth",
                "AnimalGatherYield",
                "AnimalGatherSpeed",
                "HuntingStealth"
            };

            List<string> pool = new List<string>(legacyThreePercentStats);
            if (positiveShift)
            {
                pool.AddRange(tenPercentPositiveStats);
            }

            List<string> available = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (DefDatabase<StatDef>.GetNamedSilentFail(pool[i]) != null)
                {
                    available.Add(pool[i]);
                }
            }

            if (available.Count == 0)
            {
                return "WorkSpeedGlobal";
            }

            return available.RandomElement();
        }

        private static string ResolveRandomReductionTarget()
        {
            string[] targets =
            {
                "ContextCollapseChance"
            };

            return targets.RandomElement();
        }

        private void BeginInstallTargeting()
        {
            if (parent?.MapHeld == null)
            {
                return;
            }

            EnsureInitialized();

            TargetingParameters parms = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetItems = false,
                canTargetPawns = true,
                canTargetBuildings = false,
                mapObjectTargetsMustBeAutoAttackable = false
            };
            parms.validator = target => target.HasThing && target.Thing is Pawn pawn && ClawfishUtility.IsClawfish(pawn);

            Find.Targeter.BeginTargeting(parms, target =>
            {
                Pawn pawn = target.Thing as Pawn;
                if (pawn == null || !ClawfishUtility.IsClawfish(pawn))
                {
                    Messages.Message("RimClaw_Skills_SelectClawfish".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                CreateInstallJob(pawn);
            });
        }

        private void CreateInstallJob(Pawn clawfish)
        {
            if (parent?.MapHeld == null || clawfish?.Map != parent.Map)
            {
                Messages.Message("RimClaw_Skills_NotSameMap".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Job installJob = JobMaker.MakeJob(RimClawDefOf.RimClaw_InstallSkillsMd, parent, clawfish);
            clawfish.jobs.TryTakeOrderedJob(installJob);
            Messages.Message("RimClaw_Skills_OrderedInstall".Translate(clawfish.LabelShortCap, codename), parent, MessageTypeDefOf.PositiveEvent, historical: false);
        }

        public void InstallOnPawn(Pawn pawn)
        {
            if (pawn?.health == null || !ClawfishUtility.IsClawfish(pawn))
            {
                Messages.Message("RimClaw_Skills_InstallOnlyClawfish".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(RimClawDefOf.RimClaw_SkillsImplant, pawn);
            Hediff_SkillsImplant implant = hediff as Hediff_SkillsImplant;
            if (implant != null)
            {
                implant.CopyFrom(this);
            }

            pawn.health.AddHediff(hediff);
            SkillsImplantUtility.RefreshPawnSkills(pawn);
            Messages.Message("RimClaw_Skills_Installed".Translate(codename, pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            parent.Destroy(DestroyMode.Vanish);
        }

        public void CopyFromHediff(Hediff_SkillsImplant hediff)
        {
            if (hediff == null)
            {
                return;
            }

            codename = hediff.Codename;
            effects = new List<SkillsEffectRecord>();
            for (int i = 0; i < hediff.Effects.Count; i++)
            {
                SkillsEffectRecord src = hediff.Effects[i];
                effects.Add(new SkillsEffectRecord
                {
                    Kind = src.Kind,
                    TargetDefName = src.TargetDefName,
                    LevelShift = src.LevelShift,
                    Multiplier = src.Multiplier
                });
            }
        }
    }

    public class HediffCompProperties_SkillsMdSync : HediffCompProperties
    {
        public HediffCompProperties_SkillsMdSync()
        {
            compClass = typeof(HediffComp_SkillsMdSync);
        }
    }

    public class HediffComp_SkillsMdSync : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SkillsImplantUtility.RefreshPawnSkills(parent.pawn);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            SkillsImplantUtility.RefreshPawnSkills(parent?.pawn);
        }
    }

    public class Hediff_SkillsImplant : HediffWithComps
    {
        private string codename;
        private QualityCategory qualityCategory = QualityCategory.Normal;
        private List<SkillsEffectRecord> effects = new List<SkillsEffectRecord>();

        public string Codename => codename;
        public QualityCategory Quality => qualityCategory;
        public string QualityLabel => qualityCategory.GetLabel();
        public int QualityShift => CompSkillsMd.GetQualityShift(qualityCategory);
        public List<SkillsEffectRecord> Effects => effects;

        public override string LabelBase
        {
            get
            {
                string baseName = base.LabelBase;
                if (!codename.NullOrEmpty())
                {
                    return $"{baseName} [{codename}]";
                }

                return baseName;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref codename, "codename");
            Scribe_Values.Look(ref qualityCategory, "qualityCategory", QualityCategory.Normal);
            Scribe_Collections.Look(ref effects, "effects", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && effects == null)
            {
                effects = new List<SkillsEffectRecord>();
            }

            NormalizeEffects();
        }

        public void CopyFrom(CompSkillsMd source)
        {
            codename = source.Codename;
            qualityCategory = source.CurrentQuality;
            effects = new List<SkillsEffectRecord>();
            for (int i = 0; i < source.Effects.Count; i++)
            {
                SkillsEffectRecord src = source.Effects[i];
                effects.Add(new SkillsEffectRecord
                {
                    Kind = src.Kind,
                    TargetDefName = src.TargetDefName,
                    LevelShift = src.LevelShift,
                    Multiplier = src.Multiplier
                });
            }

            NormalizeEffects();
        }

        private void NormalizeEffects()
        {
            if (effects == null || effects.Count <= 1)
            {
                return;
            }

            List<SkillsEffectRecord> normalized = new List<SkillsEffectRecord>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillsEffectRecord effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                SkillsEffectRecord existing = null;
                for (int j = 0; j < normalized.Count; j++)
                {
                    if (normalized[j].Kind == effect.Kind && normalized[j].TargetDefName == effect.TargetDefName)
                    {
                        existing = normalized[j];
                        break;
                    }
                }

                if (existing == null)
                {
                    normalized.Add(effect);
                    continue;
                }

                existing.LevelShift += effect.LevelShift;
                if (existing.Kind == SkillsEffectKind.ReductionMultiplier || existing.Kind == SkillsEffectKind.PromptInjectionResistance)
                {
                    existing.Multiplier = Mathf.Clamp(1f - existing.LevelShift * 0.03f, 0.2f, 2f);
                }
            }

            effects = normalized;
        }

        public override string TipStringExtra
        {
            get
            {
                List<string> lines = new List<string>
                {
                    $"Codename: {codename}",
                    $"Quality: {QualityLabel} ({QualityShift:+#;-#;0})",
                    "Effects:"
                };

                for (int i = 0; i < effects.Count; i++)
                {
                    lines.Add($"- {effects[i].Describe()}");
                }

                return string.Join("\n", lines);
            }
        }

        public override bool TryMergeWith(Hediff other)
        {
            // Keep each SKILLS.md implant as a distinct entry with its own codename/effects.
            return false;
        }
    }

    public static class SkillsImplantUtility
    {
        public static void RefreshPawnSkills(Pawn pawn)
        {
            if (!ClawfishUtility.IsClawfish(pawn) || pawn?.skills == null)
            {
                return;
            }

            int baseLevel = RimClawConfig.Values.clawfishSkillLevel;
            int modelAdjustment = GetModelSkillAdjustment(pawn);
            for (int i = 0; i < pawn.skills.skills.Count; i++)
            {
                SkillRecord skill = pawn.skills.skills[i];
                int shift = GetSkillShift(pawn, skill.def?.defName);
                skill.Level = Mathf.Clamp(baseLevel + modelAdjustment + shift, 0, 20);
                skill.xpSinceLastLevel = 0f;
                skill.passion = Passion.None;
            }
        }

        public static int GetModelSkillAdjustment(Pawn pawn)
        {
            CompClawfishTokenConnection connection = pawn?.TryGetComp<CompClawfishTokenConnection>();
            if (connection == null || !connection.IsConnected)
            {
                return 0;
            }

            return Mathf.Clamp(connection.GetCurrentModelSkillAdjustment(), -4, 6);
        }

        public static int GetSkillShift(Pawn pawn, string skillDefName)
        {
            int total = 0;
            foreach (SkillsEffectRecord effect in GetEffects(pawn))
            {
                if (effect.Kind == SkillsEffectKind.SkillBoost && effect.TargetDefName == skillDefName)
                {
                    total += effect.LevelShift;
                }
            }

            return total;
        }

        public static float GetAdditiveStatFactor(Pawn pawn, string statDefName)
        {
            float totalPercent = 0f;
            foreach (SkillsEffectRecord effect in GetEffects(pawn))
            {
                if (effect.Kind == SkillsEffectKind.AdditiveStatBoost && effect.TargetDefName == statDefName)
                {
                    totalPercent += effect.LevelShift * GetAdditivePercentPerPoint(statDefName);
                }
            }

            return 1f + totalPercent;
        }

        public static float GetAdditivePercentPerPoint(string statDefName)
        {
            switch (statDefName)
            {
                case "MiningYield":
                case "DeepDrillingSpeed":
                case "MiningSpeed":
                case "MedicalSurgerySuccessChance":
                case "MedicalOperationSpeed":
                case "MedicalTendQuality":
                case "MedicalTendSpeed":
                case "ConstructionSpeed":
                case "ConstructSuccessChance":
                case "ReadingSpeed":
                case "RepairSuccessChance":
                case "DrugHarvestYield":
                case "PlantHarvestYield":
                case "PlantWorkSpeed":
                case "CookingSpeed":
                case "ButcherySpeed":
                case "ButcheryEfficiency":
                case "MechanoidShreddingSpeed":
                case "MechanoidShreddingEfficiency":
                case "DrugSynthesisSpeed":
                case "DrugCookingSpeed":
                case "HackingSpeed":
                case "HackingStealth":
                case "AnimalGatherYield":
                case "AnimalGatherSpeed":
                case "HuntingStealth":
                    return 0.10f;
                default:
                    return 0.03f;
            }
        }

        public static float GetReductionFactor(Pawn pawn, string targetDefName)
        {
            // Sum all reduction points for this target, then apply once
            int totalPoints = 0;
            foreach (SkillsEffectRecord effect in GetEffects(pawn))
            {
                if (effect.TargetDefName == targetDefName && 
                    (effect.Kind == SkillsEffectKind.ReductionMultiplier || 
                     effect.Kind == SkillsEffectKind.PromptInjectionResistance))
                {
                    totalPoints += effect.LevelShift;
                }
            }

            // Apply multiplier once: 1 - (totalPoints * 0.03)
            // Will be clamped to [0.2, 2.0] range
            float multiplier = 1f - totalPoints * 0.03f;
            return Mathf.Clamp(multiplier, 0.2f, 2f);
        }

        private const float BaseTokenConsumptionIncreasePerSkillsMd = 0.05f;
        private const float BaseContextCorruptionRatePerSkillsMd = 0.005f;

        private struct ContextLoadSnapshot
        {
            public float TokenConsumptionFactor;
            public float ContextCorruptionRate;
            public float ContextCollapseFactor;
        }

        private static ContextLoadSnapshot GetContextLoadSnapshot(Pawn pawn)
        {
            ContextLoadSnapshot fallback = new ContextLoadSnapshot
            {
                TokenConsumptionFactor = 1f,
                ContextCorruptionRate = 0f,
                ContextCollapseFactor = 1f
            };

            if (pawn?.health?.hediffSet == null)
            {
                return fallback;
            }

            int skillsMdCount = GetInstalledSkillsMdCount(pawn);
            float tokenFactor = 1f + skillsMdCount * BaseTokenConsumptionIncreasePerSkillsMd;
            float corruptionRate = skillsMdCount * BaseContextCorruptionRatePerSkillsMd;
            float collapseFactor = GetReductionFactor(pawn, "ContextCollapseChance");

            if (!ClawfishUtility.IsClawfish(pawn) || RimClawDefOf.RimClaw_ContextLoad == null)
            {
                fallback.TokenConsumptionFactor = Mathf.Max(0.2f, tokenFactor);
                fallback.ContextCorruptionRate = Mathf.Max(0f, corruptionRate);
                fallback.ContextCollapseFactor = Mathf.Clamp(collapseFactor, 0.2f, 2f);
                return fallback;
            }

            Hediff_ContextLoad existing = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ContextLoad) as Hediff_ContextLoad;
            if (skillsMdCount <= 0)
            {
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                return fallback;
            }

            if (existing == null)
            {
                Hediff newHediff = HediffMaker.MakeHediff(RimClawDefOf.RimClaw_ContextLoad, pawn);
                pawn.health.AddHediff(newHediff);
                existing = newHediff as Hediff_ContextLoad;
            }

            if (existing != null)
            {
                existing.SetLoad(tokenFactor, corruptionRate, collapseFactor);
                return new ContextLoadSnapshot
                {
                    TokenConsumptionFactor = existing.TokenConsumptionFactor,
                    ContextCorruptionRate = existing.ContextCorruptionRate,
                    ContextCollapseFactor = existing.ContextCollapseFactor
                };
            }

            fallback.TokenConsumptionFactor = Mathf.Max(0.2f, tokenFactor);
            fallback.ContextCorruptionRate = Mathf.Max(0f, corruptionRate);
            fallback.ContextCollapseFactor = Mathf.Clamp(collapseFactor, 0.2f, 2f);
            return fallback;
        }

        public static float GetTokenConsumptionFactor(Pawn pawn)
        {
            return GetContextLoadSnapshot(pawn).TokenConsumptionFactor;
        }

        public static float GetContextCollapseFactor(Pawn pawn)
        {
            return GetContextLoadSnapshot(pawn).ContextCollapseFactor;
        }

        public static float GetContextCorruptionRateFromSkillsMd(Pawn pawn)
        {
            return GetContextLoadSnapshot(pawn).ContextCorruptionRate;
        }

        public static int GetInstalledSkillsMdCount(Pawn pawn)
        {
            int count = 0;
            if (pawn?.health?.hediffSet == null)
            {
                return count;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_SkillsImplant)
                {
                    count++;
                }
            }

            return count;
        }

        public static List<Hediff_SkillsImplant> GetInstalledSkillsMdList(Pawn pawn)
        {
            List<Hediff_SkillsImplant> results = new List<Hediff_SkillsImplant>();
            if (pawn?.health?.hediffSet == null)
            {
                return results;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_SkillsImplant implant)
                {
                    results.Add(implant);
                }
            }

            return results;
        }

        public static float GetPromptInjectionResistanceFactor(Pawn pawn)
        {
            float factor = 1f;
            foreach (SkillsEffectRecord effect in GetEffects(pawn))
            {
                if (effect.Kind == SkillsEffectKind.PromptInjectionResistance)
                {
                    factor *= Mathf.Clamp(effect.Multiplier, 0.2f, 1f);
                }
            }

            return factor;
        }

        public static List<SkillsEffectRecord> GetEffects(Pawn pawn)
        {
            List<SkillsEffectRecord> results = new List<SkillsEffectRecord>();
            if (pawn?.health?.hediffSet == null)
            {
                return results;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_SkillsImplant implant && implant.Effects != null)
                {
                    results.AddRange(implant.Effects);
                }
            }

            return results;
        }

        public static bool UninstallSkillsMdAtIndex(Pawn pawn, int index)
        {
            if (!ClawfishUtility.IsClawfish(pawn) || pawn?.Map == null)
            {
                return false;
            }

            List<Hediff_SkillsImplant> implants = GetInstalledSkillsMdList(pawn);
            if (index < 0 || index >= implants.Count)
            {
                return false;
            }

            Hediff_SkillsImplant implant = implants[index];
            Thing skillsMdItem = ThingMaker.MakeThing(RimClawDefOf.RimClaw_SkillsMd);
            if (skillsMdItem is ThingWithComps thingWithComps)
            {
                CompQuality qualityComp = thingWithComps.GetComp<CompQuality>();
                if (qualityComp != null)
                {
                    Traverse.Create(qualityComp).Field("quality").SetValue(implant.Quality);
                }

                CompSkillsMd skillsComp = thingWithComps.GetComp<CompSkillsMd>();
                if (skillsComp != null)
                {
                    skillsComp.CopyFromHediff(implant);
                }
            }

            GenPlace.TryPlaceThing(skillsMdItem, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            pawn.health.RemoveHediff(implant);
            RefreshPawnSkills(pawn);
            Messages.Message("RimClaw_Skills_Uninstalled".Translate(implant.Codename, pawn.LabelShortCap), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
            return true;
        }
    }
}
