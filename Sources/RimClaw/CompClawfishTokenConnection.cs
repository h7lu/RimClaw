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
        public List<string> rangedKeywords = new List<string> { "AttackStatic", "Attack", "Hunt", "CastShot", "Shoot", "Suppress", "Wait_Combat" };
        public List<string> walkingKeywords = new List<string> { "Goto", "HaulToCell", "Deliver", "Carry", "Escort", "Follow", "Flee", "Travel" };
        public List<string> idleKeywords = new List<string> { "Wait", "Wander", "Idle" };
        public List<string> heavyWorkKeywords = new List<string> { "DoBill", "Cook", "Construct", "Repair", "Mine", "CutPlant", "Harvest", "Plant", "Smooth", "Refuel", "Load" };
        public List<string> lightWorkKeywords = new List<string> { "Clean", "Handle", "Train", "Tame", "Wardening", "Social", "Art", "Smith", "Tailor", "Craft" };

        public CompProperties_ClawfishTokenConnection()
        {
            compClass = typeof(CompClawfishTokenConnection);
        }
    }

    public class CompClawfishTokenConnection : ThingComp
    {
        private Thing connectedSupplier; // Host Computer or LLM Subscription building
        private float currentTokenConsumptionRate = 0f; // tokens/s based on current job

        private CompProperties_ClawfishTokenConnection Props => (CompProperties_ClawfishTokenConnection)props;

        public Thing ConnectedSupplier => connectedSupplier;
        public bool IsConnected => connectedSupplier != null && !connectedSupplier.Destroyed;
        public float CurrentTokenConsumptionRate => currentTokenConsumptionRate;

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
            }
            else
            {
                ApplyDisconnectedShutdown(pawn);
                ApplyInsufficientIOHediff(pawn, 0f);
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

            if (pawn.Drafted)
            {
                ThingWithComps primary = pawn.equipment?.Primary;
                if (primary != null && primary.def != null && primary.def.IsRangedWeapon)
                    return Props.rangedCombatRate;
                return Props.meleeCombatRate;
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
            float ratio = currentTokenConsumptionRate > 0 ? providedRate / currentTokenConsumptionRate : 1f;
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
            string status = IsConnected ? $"Connected to {connectedSupplier.Label}" : "Not connected";
            string consumption = $"Required TPS: {currentTokenConsumptionRate:F0}";

            if (IsConnected)
            {
                float provided = GetProvidedRate(pawn);
                consumption += $"\nCurrent TPS: {provided:F0}/{currentTokenConsumptionRate:F0} needed";
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
