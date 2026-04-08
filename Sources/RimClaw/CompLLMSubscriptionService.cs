using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class SubscriptionModel
    {
        public float LifetimeSilverSpent;
        public float PendingPayment;
        public float HourlyRate;
        public List<Pawn> Claws = new List<Pawn>();
    }

    public class SubscriptionClawSnapshot
    {
        public Pawn Claw;
        public float CurrentTps;
        public float SilverPerHour;
        public List<float> SilverHistory = new List<float>();
    }

    public class SubscriptionSnapshot
    {
        public string ModelIdentifier;
        public Color ModelColor;
        public float PricePerKTokens;
        public float SpeedMultiplier;
        public float LiveThroughput;
        public float MaxCapacityPerClaw;
        public int ActiveClaws;
        public float HourlySilverRate;
        public float LifetimeSilverSpent;
        public float PendingPayment;
        public List<float> TotalSilverHistory = new List<float>();
        public List<SubscriptionClawSnapshot> Claws = new List<SubscriptionClawSnapshot>();
    }

    public class CompProperties_LLMSubscriptionService : CompProperties
    {
        public float baseTokenCapacityPerSecond = 220f;
        public float maxCapacityPerClaw = 400f;
        public float pricePerKTokens = 0.1f;
        public float silverSearchRadius = 5f;
        public int historyLength = 360;

        public CompProperties_LLMSubscriptionService()
        {
            compClass = typeof(CompLLMSubscriptionService);
        }
    }

    public class CompLLMSubscriptionService : CompTokenSupplier
    {
        private readonly SubscriptionModel model = new SubscriptionModel();
        private readonly Dictionary<int, List<float>> clawSilverHistory = new Dictionary<int, List<float>>();
        private List<float> totalSilverHistory = new List<float>();

        private float liveThroughput;
        private float speedMultiplier = 1f;
        private string modelIdentifier = "LLM Service Subscription";
        private int lastBillingTick = -1;

        public CompProperties_LLMSubscriptionService Props => (CompProperties_LLMSubscriptionService)props;
        public float SilverSearchRadius => Props.silverSearchRadius;

        public override bool IsValidSupplier => parent != null && parent.Spawned && IsPowered(parent as ThingWithComps);

        public override string TransformLabel(string label)
        {
            CompModelCard card = parent?.TryGetComp<CompModelCard>();
            if (card == null)
            {
                return label;
            }

            card.EnsureInitialized();
            string modelName = card.ModelName;
            if (string.IsNullOrEmpty(modelName))
            {
                return label;
            }

            string bracketed = $"[{modelName}]";
            if (label.Contains(bracketed))
            {
                return label;
            }

            return $"{label} {bracketed}";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref model.LifetimeSilverSpent, "lifetimeSilverSpent", 0f);
            Scribe_Values.Look(ref model.PendingPayment, "pendingSilverPayment", 0f);
            Scribe_Values.Look(ref model.HourlyRate, "hourlySilverRate", 0f);
            Scribe_Values.Look(ref liveThroughput, "liveThroughput", 0f);
            Scribe_Values.Look(ref speedMultiplier, "speedMultiplier", 1f);
            Scribe_Values.Look(ref modelIdentifier, "modelIdentifier", "LLM Subscription");
            Scribe_Collections.Look(ref totalSilverHistory, "totalSilverHistory", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (connectedClawfish == null)
                {
                    connectedClawfish = new List<Pawn>();
                }

                if (totalSilverHistory == null)
                {
                    totalSilverHistory = new List<float>();
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "Open Console",
                defaultDesc = "Open the LLM subscription management console.",
                icon = ContentFinder<Texture2D>.Get("control_panel", reportFailure: false),
                action = delegate
                {
                    Find.WindowStack.Add(new Window_LLMSubscriptionControlPanel(this));
                }
            };
        }

        public override void CompTick()
        {
            base.CompTick();

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (lastBillingTick < 0)
            {
                lastBillingTick = nowTick;
                return;
            }

            int elapsedTicks = nowTick - lastBillingTick;
            if (elapsedTicks < 60)
            {
                return;
            }

            lastBillingTick = nowTick;
            float elapsedSeconds = elapsedTicks / 60f;

            CleanupConnectedClaws();
            CleanupHistory();
            RefreshModelTelemetry();

            if (parent.IsHashIntervalTick(30))
            {
                RimClawGlowUtility.SpawnPulseGlow(parent, RimClawGlowUtility.SoftenToGlow(ResolveModelColor()), 4f);
            }

            if (!IsValidSupplier)
            {
                liveThroughput = 0f;
                model.HourlyRate = 0f;
                PushHistoryPoint();
                return;
            }

            liveThroughput = 0f;
            for (int i = 0; i < connectedClawfish.Count; i++)
            {
                Pawn claw = connectedClawfish[i];
                liveThroughput += GetCurrentNeededRate(claw);
            }

            float supplied = Mathf.Min(liveThroughput, totalTokenCapacityPerSecond);
            float silverPerToken = Props.pricePerKTokens / 1000f;
            float silverPerSecond = supplied * silverPerToken;
            model.HourlyRate = silverPerSecond * 3600f;

            model.PendingPayment += silverPerSecond * elapsedSeconds;
            model.LifetimeSilverSpent += silverPerSecond * elapsedSeconds;

            TryAutoPayIntegerSilver();
            PushHistoryPoint();
            PushPerClawHistory();
        }

        public override float GetAvailableTokenRateForClawfish(Pawn claw)
        {
            if (claw == null || !connectedClawfish.Contains(claw) || connectedClawfish.Count == 0)
            {
                return 0f;
            }

            float shared = totalTokenCapacityPerSecond / connectedClawfish.Count;
            return Mathf.Min(shared, Props.maxCapacityPerClaw);
        }

        public void DisconnectClaw(Pawn claw)
        {
            if (claw == null)
            {
                return;
            }

            RemoveConnectedClaw(claw);
            CompClawfishTokenConnection connection = claw.TryGetComp<CompClawfishTokenConnection>();
            if (connection != null && connection.ConnectedSupplier == parent)
            {
                connection.Disconnect();
            }
        }

        public SubscriptionSnapshot GetSnapshot()
        {
            RefreshModelTelemetry();

            float currentLiveThroughput = 0f;
            for (int i = 0; i < connectedClawfish.Count; i++)
            {
                Pawn claw = connectedClawfish[i];
                currentLiveThroughput += GetCurrentNeededRate(claw);
            }

            float supplied = Mathf.Min(currentLiveThroughput, totalTokenCapacityPerSecond);
            float silverPerToken = Props.pricePerKTokens / 1000f;
            float currentHourlyRate = supplied * silverPerToken * 3600f;

            SubscriptionSnapshot snapshot = new SubscriptionSnapshot
            {
                ModelIdentifier = GetCurrentModelIdentifier(),
                ModelColor = ResolveModelColor(),
                PricePerKTokens = Props.pricePerKTokens,
                SpeedMultiplier = speedMultiplier,
                LiveThroughput = currentLiveThroughput,
                MaxCapacityPerClaw = Props.maxCapacityPerClaw,
                ActiveClaws = connectedClawfish.Count,
                HourlySilverRate = currentHourlyRate,
                LifetimeSilverSpent = model.LifetimeSilverSpent,
                PendingPayment = model.PendingPayment
            };

            snapshot.TotalSilverHistory.AddRange(totalSilverHistory);

            for (int i = 0; i < connectedClawfish.Count; i++)
            {
                Pawn claw = connectedClawfish[i];
                if (claw == null || claw.Destroyed || claw.Dead)
                {
                    continue;
                }

                float currentTps = GetCurrentNeededRate(claw);
                float provided = GetAvailableTokenRateForClawfish(claw);
                float effectiveTps = Mathf.Min(currentTps, provided);
                float silverPerHour = effectiveTps * (Props.pricePerKTokens / 1000f) * 3600f;

                SubscriptionClawSnapshot clawSnapshot = new SubscriptionClawSnapshot
                {
                    Claw = claw,
                    CurrentTps = currentTps,
                    SilverPerHour = silverPerHour
                };

                if (clawSilverHistory.TryGetValue(claw.thingIDNumber, out List<float> history) && history != null)
                {
                    clawSnapshot.SilverHistory.AddRange(history);
                }

                snapshot.Claws.Add(clawSnapshot);
            }

            return snapshot;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(parent.Position, Props.silverSearchRadius);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent?.Spawned != true || !IsPowered(parent as ThingWithComps))
            {
                return;
            }

            Color glowColor = ResolveModelColor();
            RimClawGlowUtility.DrawGlow(parent.DrawPos, RimClawGlowUtility.SoftenToGlow(glowColor), 4f);
        }

        public override string CompInspectStringExtra()
        {
            float supplied = Mathf.Min(liveThroughput, totalTokenCapacityPerSecond);
            return $"Model: {GetCurrentModelIdentifier()}\nLive Throughput: {liveThroughput:0.0}/{totalTokenCapacityPerSecond:0.0} TPS\nActive Claws: {connectedClawfish.Count}\nHourly Silver: {model.HourlyRate:0.00}\nPending Payment: {model.PendingPayment:0.00}\nSupplied TPS: {supplied:0.0}";
        }

        private void RefreshModelTelemetry()
        {
            CompModelCard card = parent.TryGetComp<CompModelCard>();
            if (card == null)
            {
                totalTokenCapacityPerSecond = Props.baseTokenCapacityPerSecond;
                speedMultiplier = 1f;
                modelIdentifier = parent.LabelCap;
                return;
            }

            card.EnsureInitialized();
            totalTokenCapacityPerSecond = Mathf.Max(1f, Props.baseTokenCapacityPerSecond * card.WorkSpeedMultiplier);
            speedMultiplier = Mathf.Max(0.1f, card.WorkSpeedMultiplier);
            modelIdentifier = card.ModelName;
        }

        private string GetCurrentModelIdentifier()
        {
            CompModelCard card = parent?.TryGetComp<CompModelCard>();
            if (card != null)
            {
                card.EnsureInitialized();
                if (!string.IsNullOrEmpty(card.ModelName))
                {
                    return card.ModelName;
                }
            }

            return modelIdentifier;
        }

        private Color ResolveModelColor()
        {
            CompModelCard card = parent.TryGetComp<CompModelCard>();
            if (card == null)
            {
                return Color.white;
            }

            card.EnsureInitialized();
            return card.ModelColor;
        }

        private float GetCurrentNeededRate(Pawn claw)
        {
            if (claw == null)
            {
                return 0f;
            }

            CompClawfishTokenConnection connection = claw.TryGetComp<CompClawfishTokenConnection>();
            if (connection != null)
            {
                return connection.CurrentTokenConsumptionRate;
            }

            CompClawfishToken legacy = claw.TryGetComp<CompClawfishToken>();
            if (legacy != null)
            {
                return legacy.GetCurrentConsumptionRatePerSecond(claw);
            }

            return 0f;
        }

        private void PushHistoryPoint()
        {
            totalSilverHistory.Add(model.HourlyRate);
            int maxLen = Mathf.Max(60, Props.historyLength);
            if (totalSilverHistory.Count > maxLen)
            {
                totalSilverHistory.RemoveAt(0);
            }
        }

        private void PushPerClawHistory()
        {
            int maxLen = Mathf.Max(60, Props.historyLength);

            for (int i = 0; i < connectedClawfish.Count; i++)
            {
                Pawn claw = connectedClawfish[i];
                if (claw == null)
                {
                    continue;
                }

                float currentTps = GetCurrentNeededRate(claw);
                float provided = GetAvailableTokenRateForClawfish(claw);
                float effectiveTps = Mathf.Min(currentTps, provided);
                float silverPerHour = effectiveTps * (Props.pricePerKTokens / 1000f) * 3600f;

                if (!clawSilverHistory.TryGetValue(claw.thingIDNumber, out List<float> history) || history == null)
                {
                    history = new List<float>();
                    clawSilverHistory[claw.thingIDNumber] = history;
                }

                history.Add(silverPerHour);
                if (history.Count > maxLen)
                {
                    history.RemoveAt(0);
                }
            }
        }

        private void CleanupHistory()
        {
            List<int> removeKeys = null;
            foreach (KeyValuePair<int, List<float>> kvp in clawSilverHistory)
            {
                bool stillConnected = false;
                for (int i = 0; i < connectedClawfish.Count; i++)
                {
                    Pawn claw = connectedClawfish[i];
                    if (claw != null && claw.thingIDNumber == kvp.Key)
                    {
                        stillConnected = true;
                        break;
                    }
                }

                if (!stillConnected)
                {
                    if (removeKeys == null)
                    {
                        removeKeys = new List<int>();
                    }

                    removeKeys.Add(kvp.Key);
                }
            }

            if (removeKeys != null)
            {
                for (int i = 0; i < removeKeys.Count; i++)
                {
                    clawSilverHistory.Remove(removeKeys[i]);
                }
            }
        }

        private void CleanupConnectedClaws()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int i = connectedClawfish.Count - 1; i >= 0; i--)
            {
                Pawn pawn = connectedClawfish[i];
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Map != parent.MapHeld || !seen.Add(pawn.thingIDNumber))
                {
                    connectedClawfish.RemoveAt(i);
                }
            }
        }

        private void TryAutoPayIntegerSilver()
        {
            int owed = Mathf.FloorToInt(model.PendingPayment);
            if (owed <= 0 || parent.MapHeld == null)
            {
                return;
            }

            int paid = ConsumeNearbySilver(owed);
            if (paid > 0)
            {
                model.PendingPayment -= paid;
                model.PendingPayment = Mathf.Max(0f, model.PendingPayment);
            }
        }

        private int ConsumeNearbySilver(int amount)
        {
            int remaining = amount;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.MapHeld, Props.silverSearchRadius, useCenter: true))
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (thing.def != ThingDefOf.Silver)
                {
                    continue;
                }

                int take = Mathf.Min(remaining, thing.stackCount);
                if (take <= 0)
                {
                    continue;
                }

                thing.SplitOff(take).Destroy(DestroyMode.Vanish);
                remaining -= take;
            }

            return amount - remaining;
        }

        private static bool IsPowered(ThingWithComps thing)
        {
            if (thing == null)
            {
                return false;
            }

            CompPowerTrader power = thing.GetComp<CompPowerTrader>();
            return power == null || power.PowerOn;
        }
    }
}
