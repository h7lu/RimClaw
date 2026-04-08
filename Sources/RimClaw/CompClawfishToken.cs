using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class CompProperties_ClawfishToken : CompProperties
    {
        public float maxTokens = 1200f;
        public float consumptionMultiplier = 1f;

        public float noJobRate = 0.05f;
        public float defaultRate = 0.4f;
        public float idleRate = 0.08f;
        public float combatRate = 1.5f;
        public float workRate = 1.1f;

        public string onlineStatusText = "Online";
        public string depletedStatusText = "Errorcode 402";

        public List<string> idleJobKeywords = new List<string> { "Goto", "Wait", "Wander" };
        public List<string> combatJobKeywords = new List<string> { "Attack", "Hunt" };
        public List<string> workJobKeywords = new List<string> { "Research", "DoBill", "Tend", "Operate", "Cook", "Construct", "Mine" };

        public CompProperties_ClawfishToken()
        {
            compClass = typeof(CompClawfishToken);
        }
    }

    public class CompClawfishToken : ThingComp
    {
        private float currentTokens;
        private int consecutiveWorkTicks;
        private int lastSelfReprogramTick;

        public CompProperties_ClawfishToken Props => (CompProperties_ClawfishToken)props;

        public float CurrentTokens => currentTokens;

        public float MaxTokens => Math.Max(1f, Props.maxTokens);

        public float ConsumptionMultiplier => Math.Max(0f, Props.consumptionMultiplier);

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            if (currentTokens <= 0f)
            {
                currentTokens = MaxTokens;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentTokens, "currentTokens", -1f);
            Scribe_Values.Look(ref consecutiveWorkTicks, "consecutiveWorkTicks", 0);
            Scribe_Values.Look(ref lastSelfReprogramTick, "lastSelfReprogramTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && currentTokens < 0f)
            {
                currentTokens = MaxTokens;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!parent.IsHashIntervalTick(60))
            {
                return;
            }

            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !ClawfishUtility.IsClawfish(pawn))
            {
                return;
            }

            float rate = GetCurrentConsumptionRatePerSecond(pawn);
            TrackContinuousWorkAndMentalRisk(pawn);
            TryStartSelfReprogramming(pawn);

            if (rate > 0f)
            {
                currentTokens = Math.Max(0f, currentTokens - rate);
            }

            bool depleted = currentTokens <= 0.0001f;
            Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_TokenDepleted);
            if (depleted)
            {
                if (existing == null)
                {
                    pawn.health?.AddHediff(RimClawDefOf.RimClaw_TokenDepleted);
                }
            }
            else if (existing != null)
            {
                pawn.health?.RemoveHediff(existing);
            }
        }

        private void TrackContinuousWorkAndMentalRisk(Pawn pawn)
        {
            string jobName = pawn.CurJob?.def?.defName ?? string.Empty;
            bool idleLike = ContainsAny(jobName, Props.idleJobKeywords);
            if (idleLike)
            {
                consecutiveWorkTicks = 0;
                return;
            }

            consecutiveWorkTicks += 60;
            var cfg = RimClawConfig.Values;
            if (consecutiveWorkTicks < cfg.contextCollapseRollStartTicks)
            {
                return;
            }

            if (pawn.MentalStateDef == RimClawDefOf.RimClaw_ContextCollapse)
            {
                return;
            }

            float reducedChance = cfg.contextCollapseChancePerSecond * Mathf.Max(0.2f, SkillsImplantUtility.GetContextCollapseFactor(pawn));
            if (Rand.Chance(reducedChance))
            {
                pawn.mindState?.mentalStateHandler?.TryStartMentalState(RimClawDefOf.RimClaw_ContextCollapse, forceWake: true);
                consecutiveWorkTicks = 0;
            }
        }

        private void TryStartSelfReprogramming(Pawn pawn)
        {
            if (Find.TickManager.TicksGame - lastSelfReprogramTick < 1200)
            {
                return;
            }

            if (pawn.Faction != Faction.OfPlayer || pawn.Downed || pawn.CurJobDef == RimClawDefOf.RimClaw_SelfReprogramming)
            {
                return;
            }

            bool injured = false;
            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                if (pawn.health.hediffSet.hediffs[i] is Hediff_Injury injury && !injury.IsPermanent())
                {
                    injured = true;
                    break;
                }
            }

            if (!injured)
            {
                return;
            }

            Job job = JobMaker.MakeJob(RimClawDefOf.RimClaw_SelfReprogramming);
            pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
            lastSelfReprogramTick = Find.TickManager.TicksGame;
        }

        public float GetCurrentConsumptionRatePerSecond(Pawn pawn)
        {
            if (pawn.CurJob == null)
            {
                return Props.noJobRate * ConsumptionMultiplier;
            }

            string jobName = pawn.CurJob.def?.defName ?? string.Empty;
            float baseRate = Props.defaultRate;

            if (ContainsAny(jobName, Props.idleJobKeywords))
            {
                baseRate = Props.idleRate;
            }
            else if (pawn.Drafted || ContainsAny(jobName, Props.combatJobKeywords))
            {
                baseRate = Props.combatRate;
            }
            else if (ContainsAny(jobName, Props.workJobKeywords))
            {
                baseRate = Props.workRate;
            }

            float skillsFactor = Mathf.Max(0.2f, SkillsImplantUtility.GetTokenConsumptionFactor(pawn));
            return baseRate * ConsumptionMultiplier * skillsFactor;
        }

        private static bool ContainsAny(string value, List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < keywords.Count; i++)
            {
                string keyword = keywords[i];
                if (!string.IsNullOrWhiteSpace(keyword) && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddTokens(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentTokens = Math.Min(MaxTokens, currentTokens + amount);
        }

        public void ForceDepleted()
        {
            currentTokens = 0f;
        }

        public override string CompInspectStringExtra()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !ClawfishUtility.IsClawfish(pawn))
            {
                return null;
            }

            float rate = GetCurrentConsumptionRatePerSecond(pawn);
            string status = currentTokens > 0.0001f ? Props.onlineStatusText : Props.depletedStatusText;
            return "RimClaw_ClawfishToken_Inspect".Translate(currentTokens.ToString("0.0"), MaxTokens.ToString("0.0"), rate.ToString("0.00"), status);
        }
    }
}
