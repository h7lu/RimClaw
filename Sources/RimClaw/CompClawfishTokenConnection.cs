using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_ClawfishTokenConnection : CompProperties
    {
        public float idleRate = 50f;
        public float walkingRate = 100f;
        public float rangedCombatRate = 400f;
        public float meleeCombatRate = 500f;
        public float draftedIdleRate = 130f;
        public float organHarvestRate = 800f;
        public float surgeryRate = 600f;
        public float medicalHumanRate = 400f;
        public float medicalOtherRate = 300f;
        public float researchRate = 1000f;
        public float heavyWorkRate = 250f;
        public float lightWorkRate = 200f;
        public float defaultRate = 150f;

        public List<string> researchKeywords = new List<string> { "Research" };
        public List<string> surgeryKeywords = new List<string> { "Operate", "Surgery", "MedicalOperation" };
        public List<string> organHarvestKeywords = new List<string> { "Harvest", "Extract" };
        public List<string> medicalKeywords = new List<string> { "Tend", "Doctor", "Treat" };
        public List<string> meleeKeywords = new List<string> { "AttackMelee", "Melee", "BeatFire", "HuntMelee" };
        public List<string> rangedKeywords = new List<string> { "AttackStatic", "Attack", "Hunt", "CastShot", "Shoot", "Suppress" };
        public List<string> draftedIdleKeywords = new List<string> { "Wait_Combat" };
        public List<string> walkingKeywords = new List<string> { "Goto", "HaulToCell", "Deliver", "Carry", "Escort", "Follow", "Flee", "Travel" };
        public List<string> idleKeywords = new List<string> { "Wait", "Wander", "Idle" };
        public List<string> heavyWorkKeywords = new List<string> { "DoBill", "Cook", "Construct", "Repair", "Mine", "CutPlant", "Harvest", "Plant", "Smooth", "Refuel", "Load" };
        public List<string> lightWorkKeywords = new List<string> { "Clean", "Handle", "Train", "Tame", "Wardening", "Social", "Art", "Smith", "Tailor", "Craft" };

        public string connectedStatusFormat = "Connected to {0}";
        public string notConnectedStatusText = "Not connected";
        public string requiredTpsFormat = "Required TPS: {0}";
        public string currentTpsFormat = "Current TPS: {0}/{1} needed";

        public CompProperties_ClawfishTokenConnection()
        {
            compClass = typeof(CompClawfishTokenConnection);
        }
    }

    public class CompClawfishTokenConnection : ThingComp
    {
        private Thing connectedSupplier; // Host Computer or LLM Subscription building
        private float currentTokenConsumptionRate = 0f; // tokens/s based on current job
        private int lastAppliedModelSkillAdjustment = int.MinValue;

        private CompProperties_ClawfishTokenConnection Props => (CompProperties_ClawfishTokenConnection)props;

        public Thing ConnectedSupplier => connectedSupplier;
        public bool IsConnected => connectedSupplier != null && !connectedSupplier.Destroyed;
        public float CurrentTokenConsumptionRate => currentTokenConsumptionRate;
        public float EffectiveTokenConsumptionRate => GetAdjustedTokenConsumptionRate(parent as Pawn);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref connectedSupplier, "connectedSupplier");
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !ClawfishUtility.IsClawfishColonist(pawn))
            {
                return;
            }

            // Clean up dead/destroyed suppliers
            if (connectedSupplier != null && connectedSupplier.Destroyed)
            {
                Disconnect();
            }

            // Update consumption rate based on current job
            currentTokenConsumptionRate = GetCurrentTokenConsumptionRate(pawn);

            int currentModelSkillAdjustment = GetModelSkillAdjustment(pawn);
            if (currentModelSkillAdjustment != lastAppliedModelSkillAdjustment)
            {
                lastAppliedModelSkillAdjustment = currentModelSkillAdjustment;
                SkillsImplantUtility.RefreshPawnSkills(pawn);
            }

            // Check if we have sufficient I/O rate from supplier
            if (IsConnected)
            {
                float providedRate = GetProvidedRate(pawn);
                if (providedRate > 0.01f)
                {
                    RemoveDisconnectedShutdown(pawn);
                }
                else
                {
                    ApplyDisconnectedShutdown(pawn);
                }

                ApplyInsufficientIOHediff(pawn, providedRate);
                ApplyModelWorkSpeedBonusHediff(pawn);
            }
            else
            {
                ApplyDisconnectedShutdown(pawn);
                ApplyInsufficientIOHediff(pawn, 0f);
                RemoveModelWorkSpeedBonusHediff(pawn);
            }
        }

        private float GetCurrentTokenConsumptionRate(Pawn pawn)
        {
            if (pawn.CurJob == null)
            {
                return Props.idleRate;
            }

            string jobName = pawn.CurJob.def?.defName ?? string.Empty;

            // Highest-cost production and surgical work.
            if (ContainsAny(jobName, Props.researchKeywords))
                return Props.researchRate;

            if (ContainsAny(jobName, Props.surgeryKeywords))
            {
                string recipeName = pawn.CurJob.bill?.recipe?.defName ?? string.Empty;
                if (ContainsAny(recipeName, Props.organHarvestKeywords))
                    return Props.organHarvestRate;
                return Props.surgeryRate;
            }

            // Direct tending on humanlike patients.
            if (ContainsAny(jobName, Props.medicalKeywords))
            {
                Pawn patient = pawn.CurJob.targetA.Thing as Pawn;
                if (patient != null && patient.RaceProps != null && patient.RaceProps.Humanlike)
                    return Props.medicalHumanRate;
                return Props.medicalOtherRate;
            }

            // Combat split: melee > ranged.
            if (ContainsAny(jobName, Props.meleeKeywords))
                return Props.meleeCombatRate;

            if (ContainsAny(jobName, Props.rangedKeywords))
                return Props.rangedCombatRate;

            if (pawn.Drafted && ContainsAny(jobName, Props.draftedIdleKeywords))
                return Props.draftedIdleRate;

            if (pawn.Drafted)
            {
                // Drafted but not actively in combat/special work should stay at low movement/standby cost.
                return Props.walkingRate;
            }

            // Motion and logistics.
            if (ContainsAny(jobName, Props.walkingKeywords))
                return Props.walkingRate;

            if (ContainsAny(jobName, Props.idleKeywords))
                return Props.idleRate;

            // General work buckets.
            if (ContainsAny(jobName, Props.heavyWorkKeywords))
                return Props.heavyWorkRate;

            if (ContainsAny(jobName, Props.lightWorkKeywords))
                return Props.lightWorkRate;

            // Fallback for uncategorized jobs.
            return Props.defaultRate;
        }

        private static bool ContainsAny(string value, IEnumerable<string> keywords)
        {
            if (string.IsNullOrEmpty(value) || keywords == null)
                return false;

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void ApplyDisconnectedShutdown(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_TokenDepleted);
            if (existing == null)
            {
                pawn.health.AddHediff(RimClawDefOf.RimClaw_TokenDepleted);
            }
        }

        private static void RemoveDisconnectedShutdown(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_TokenDepleted);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }

        private void ApplyInsufficientIOHediff(Pawn pawn, float providedRate)
        {
            float adjustedRequiredRate = GetAdjustedTokenConsumptionRate(pawn);
            float ratio = adjustedRequiredRate > 0 ? providedRate / adjustedRequiredRate : 1f;
            ratio = Mathf.Clamp01(ratio); // 0 to 1

            Hediff_InsufficientIORate existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_InsufficientIORate) as Hediff_InsufficientIORate;

            if (ratio < 1f)
            {
                // Need the hediff
                if (existing == null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(RimClawDefOf.RimClaw_InsufficientIORate, pawn);
                    pawn.health?.AddHediff(hediff);
                    existing = hediff as Hediff_InsufficientIORate;
                }

                if (existing != null)
                {
                    existing.SetThrottleRatio(ratio);
                }
            }
            else if (existing != null)
            {
                pawn.health?.RemoveHediff(existing);
            }
        }

        private void ApplyModelWorkSpeedBonusHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            float modelMultiplier = GetModelWorkSpeedMultiplier();
            Hediff_ModelWorkSpeedBonus existing = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ModelWorkSpeedBonus) as Hediff_ModelWorkSpeedBonus;

            if (modelMultiplier > 1.001f)
            {
                // Need the hediff if multiplier is noticeably above 1.0
                if (existing == null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(RimClawDefOf.RimClaw_ModelWorkSpeedBonus, pawn);
                    pawn.health.AddHediff(hediff);
                    existing = hediff as Hediff_ModelWorkSpeedBonus;
                }

                if (existing != null)
                {
                    existing.SetWorkSpeedMultiplier(modelMultiplier);
                }
            }
            else if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }

        private void RemoveModelWorkSpeedBonusHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(RimClawDefOf.RimClaw_ModelWorkSpeedBonus);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }

        private float GetModelWorkSpeedMultiplier()
        {
            if (connectedSupplier == null || connectedSupplier.Destroyed)
                return 1f;

            CompLLMSubscriptionService subscription = connectedSupplier.TryGetComp<CompLLMSubscriptionService>();
            if (subscription != null)
            {
                return subscription.GetSpeedMultiplier();
            }

            CompHostComputerService host = connectedSupplier.TryGetComp<CompHostComputerService>();
            if (host != null)
            {
                return host.GetModelWorkSpeedMultiplier();
            }

            return 1f;
        }

        public int GetCurrentModelSkillAdjustment()
        {
            return GetModelSkillAdjustment(parent as Pawn);
        }

        private int GetModelSkillAdjustment(Pawn pawn)
        {
            if (connectedSupplier == null || connectedSupplier.Destroyed || pawn == null)
            {
                return 0;
            }

            CompHostComputerService host = connectedSupplier.TryGetComp<CompHostComputerService>();
            if (host != null)
            {
                return host.GetModelSkillAdjustmentForClaw(pawn);
            }

            CompModelCard modelCard = connectedSupplier.TryGetComp<CompModelCard>();
            if (modelCard != null)
            {
                modelCard.EnsureInitialized();
                return modelCard.ModelSkillLevelAdjustment;
            }

            return 0;
        }

        public void Connect(Thing supplier)
        {
            if (supplier == null || supplier.Destroyed)
                return;

            if (!IsValidSupplierThing(supplier))
                return;

            if (connectedSupplier != null && connectedSupplier != supplier)
            {
                Pawn pawn = parent as Pawn;
                if (pawn != null)
                {
                    CompHostComputerService oldHost = connectedSupplier.TryGetComp<CompHostComputerService>();
                    if (oldHost != null)
                    {
                        oldHost.RemoveConnectedClaw(pawn);
                    }

                    CompTokenSupplier oldSupplier = connectedSupplier.TryGetComp<CompTokenSupplier>();
                    if (oldSupplier != null)
                    {
                        oldSupplier.RemoveConnectedClaw(pawn);
                    }
                }
            }

            connectedSupplier = supplier;
        }

        public void Disconnect()
        {
            connectedSupplier = null;
            Pawn pawn = parent as Pawn;
            if (pawn != null)
            {
                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(RimClawDefOf.RimClaw_InsufficientIORate);
                if (hediff != null)
                {
                    pawn.health?.RemoveHediff(hediff);
                }

                ApplyDisconnectedShutdown(pawn);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!ClawfishUtility.IsClawfishColonist(parent as Pawn))
                return null;

            Pawn pawn = parent as Pawn;
            string status = IsConnected ? string.Format(Props.connectedStatusFormat, connectedSupplier.Label) : Props.notConnectedStatusText;
            float required = GetAdjustedTokenConsumptionRate(pawn);
            string consumption = string.Format(Props.requiredTpsFormat, required.ToString("F0"));

            if (IsConnected)
            {
                float provided = GetProvidedRate(pawn);
                consumption += "\n" + string.Format(Props.currentTpsFormat, provided.ToString("F0"), required.ToString("F0"));
            }

            return $"{status}\n{consumption}";
        }

        private float GetProvidedRate(Pawn pawn)
        {
            if (connectedSupplier == null || connectedSupplier.Destroyed)
            {
                return 0f;
            }

            CompHostComputerService host = connectedSupplier.TryGetComp<CompHostComputerService>();
            if (host != null)
            {
                return host.GetAvailableTokenRateForClawfish(pawn);
            }

            CompTokenSupplier supplier = connectedSupplier.TryGetComp<CompTokenSupplier>();
            if (supplier != null)
            {
                return supplier.GetAvailableTokenRateForClawfish(pawn);
            }

            return 0f;
        }

        public float GetAdjustedTokenConsumptionRate(Pawn pawn)
        {
            if (pawn == null)
            {
                return currentTokenConsumptionRate;
            }

            float factor = Mathf.Max(0.2f, SkillsImplantUtility.GetTokenConsumptionFactor(pawn));
            return currentTokenConsumptionRate * factor;
        }

        private static bool IsValidSupplierThing(Thing supplierThing)
        {
            if (supplierThing == null || supplierThing.Destroyed)
            {
                return false;
            }

            CompHostComputerService host = supplierThing.TryGetComp<CompHostComputerService>();
            if (host != null)
            {
                return host.IsValidSupplier;
            }

            CompTokenSupplier supplier = supplierThing.TryGetComp<CompTokenSupplier>();
            return supplier != null && supplier.IsValidSupplier;
        }
    }
}
