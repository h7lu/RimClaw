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
        public float PricePerMTokens;
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
        public float maxCapacityPerClaw = 80f;
        public float pricePerMillionTokens = 8f;
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
        private string modelIdentifier = "LLM Subscription";

        public CompProperties_LLMSubscriptionService Props => (CompProperties_LLMSubscriptionService)props;
        public float SilverSearchRadius => Props.silverSearchRadius;

        public override bool IsValidSupplier => parent != null && parent.Spawned && IsPowered(parent as ThingWithComps);

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
                defaultLabel = "Open Control Panel",
                defaultDesc = "Open the LLM subscription management console.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/TogglePower", reportFailure: false),
                action = delegate
                {
                    Find.WindowStack.Add(new Window_LLMSubscriptionControlPanel(this));
                }
            };
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(60))
            {
                return;
            }

            CleanupConnectedClaws();
            CleanupHistory();
            RefreshModelTelemetry();

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
            float silverPerToken = Props.pricePerMillionTokens / 1000000f;
            float silverPerSecond = supplied * silverPerToken;
            model.HourlyRate = silverPerSecond * 3600f;

            model.PendingPayment += silverPerSecond;
            model.LifetimeSilverSpent += silverPerSecond;

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
            SubscriptionSnapshot snapshot = new SubscriptionSnapshot
            {
                ModelIdentifier = modelIdentifier,
                PricePerMTokens = Props.pricePerMillionTokens,
                SpeedMultiplier = speedMultiplier,
                LiveThroughput = liveThroughput,
                MaxCapacityPerClaw = Props.maxCapacityPerClaw,
                ActiveClaws = connectedClawfish.Count,
                HourlySilverRate = model.HourlyRate,
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
                float silverPerHour = effectiveTps * (Props.pricePerMillionTokens / 1000000f) * 3600f;

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

        public override string CompInspectStringExtra()
        {
            float supplied = Mathf.Min(liveThroughput, totalTokenCapacityPerSecond);
            return $"Model: {modelIdentifier}\nLive Throughput: {liveThroughput:0.0}/{totalTokenCapacityPerSecond:0.0} TPS\nActive Claws: {connectedClawfish.Count}\nHourly Silver: {model.HourlyRate:0.00}\nPending Payment: {model.PendingPayment:0.00}\nSupplied TPS: {supplied:0.0}";
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
            totalTokenCapacityPerSecond = Mathf.Max(1f, card.TokenPerSecondPerInstance);
            speedMultiplier = Mathf.Max(0.1f, card.WorkSpeedMultiplier);
            modelIdentifier = card.ModelName;
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
            totalSilverHistory.Add(model.LifetimeSilverSpent);
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
                float silverPerHour = effectiveTps * (Props.pricePerMillionTokens / 1000000f) * 3600f;

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
