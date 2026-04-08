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
        public float PerSecondRate;
        public List<Pawn> Claws = new List<Pawn>();
    }

    public class SubscriptionClawSnapshot
    {
        public Pawn Claw;
        public float CurrentTps;
        public float ProvidedTps;
        public float SilverPerSecond;
        public List<float> SilverPerSecondHistory = new List<float>();
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
        public float PerSecondSilverRate;
        public float CurrentHourSilverSpent;
        public float LifetimeSilverSpent;
        public float PendingPayment;
        public List<float> HourlySilverHistory = new List<float>();
        public List<SubscriptionClawSnapshot> Claws = new List<SubscriptionClawSnapshot>();
    }

    public class CompProperties_LLMSubscriptionService : CompProperties
    {
        public float baseTokenCapacityPerSecond = 440f;
        public float maxCapacityPerClaw = 400f;
        public float pricePerKTokens = 0.5f;
        public float silverSearchRadius = 5f;
        public int historyLength = 360;

        public CompProperties_LLMSubscriptionService()
        {
            compClass = typeof(CompLLMSubscriptionService);
        }
    }

    public class CompLLMSubscriptionService : CompTokenSupplier
    {
        private const float InGameSecondsPerHour = 2500f / 60f;

        private readonly SubscriptionModel model = new SubscriptionModel();
        private readonly Dictionary<int, List<float>> clawSilverHistory = new Dictionary<int, List<float>>();
        private List<float> hourlySilverHistory = new List<float>();

        private float liveThroughput;
        private float speedMultiplier = 1f;
        private string modelIdentifier = "LLM Service Subscription";
        private int lastBillingTick = -1;
        private float currentHourSilverSpent;
        private float currentHourElapsedGameSeconds;
        private float currentPerSecondSilverRate;
        private bool serviceStopped;

        public bool IsOutOfFee => serviceStopped;

        public CompProperties_LLMSubscriptionService Props => (CompProperties_LLMSubscriptionService)props;
        public float SilverSearchRadius => Props.silverSearchRadius;

        public override bool IsValidSupplier => parent != null && parent.Spawned && IsPowered(parent as ThingWithComps);

        public float GetSpeedMultiplier() => speedMultiplier;

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
            Scribe_Values.Look(ref model.PerSecondRate, "perSecondSilverRate", 0f);
            Scribe_Values.Look(ref liveThroughput, "liveThroughput", 0f);
            Scribe_Values.Look(ref speedMultiplier, "speedMultiplier", 1f);
            Scribe_Values.Look(ref modelIdentifier, "modelIdentifier", "LLM Subscription");
            Scribe_Values.Look(ref currentHourSilverSpent, "currentHourSilverSpent", 0f);
            Scribe_Values.Look(ref currentHourElapsedGameSeconds, "currentHourElapsedGameSeconds", 0f);
            Scribe_Values.Look(ref serviceStopped, "serviceStopped", false);
            Scribe_Collections.Look(ref hourlySilverHistory, "hourlySilverHistory", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (connectedClawfish == null)
                {
                    connectedClawfish = new List<Pawn>();
                }

                if (hourlySilverHistory == null)
                {
                    hourlySilverHistory = new List<float>();
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "RimClaw_Subscription_OpenConsole_Label".Translate(),
                defaultDesc = "RimClaw_Subscription_OpenConsole_Desc".Translate(),
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

            if (parent != null && parent.IsHashIntervalTick(60))
            {
                UpdateGlowerColor();
            }

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

            if (serviceStopped)
            {
                liveThroughput = 0f;
                currentPerSecondSilverRate = 0f;
                model.PerSecondRate = 0f;

                if (IsValidSupplier)
                {
                    TryAutoPayIntegerSilver(stopOnFailure: false);
                    if (model.PendingPayment < 1f)
                    {
                        ResumeServiceAfterPayment();
                    }
                }

                return;
            }

            if (parent.IsHashIntervalTick(30))
            {
                RimClawGlowUtility.SpawnPulseGlow(parent, RimClawGlowUtility.SoftenToGlow(ResolveModelColor()), 4f);
            }

            if (!IsValidSupplier)
            {
                liveThroughput = 0f;
                model.PerSecondRate = 0f;
                TickHourlyHistory(0f, elapsedSeconds);
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
            currentPerSecondSilverRate = silverPerSecond;
            model.PerSecondRate = silverPerSecond;

            model.PendingPayment += silverPerSecond * elapsedSeconds;
            model.LifetimeSilverSpent += silverPerSecond * elapsedSeconds;

            TickHourlyHistory(silverPerSecond * elapsedSeconds, elapsedSeconds);

            TryAutoPayIntegerSilver(stopOnFailure: true);
            PushPerClawHistory();
        }

        public override float GetAvailableTokenRateForClawfish(Pawn claw)
        {
            if (serviceStopped || claw == null || !connectedClawfish.Contains(claw) || connectedClawfish.Count == 0)
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
            float currentPerSecondRate = supplied * silverPerToken;

            SubscriptionSnapshot snapshot = new SubscriptionSnapshot
            {
                ModelIdentifier = GetCurrentModelIdentifier(),
                ModelColor = ResolveModelColor(),
                PricePerKTokens = Props.pricePerKTokens,
                SpeedMultiplier = speedMultiplier,
                LiveThroughput = currentLiveThroughput,
                MaxCapacityPerClaw = Props.maxCapacityPerClaw,
                ActiveClaws = connectedClawfish.Count,
                PerSecondSilverRate = currentPerSecondRate,
                CurrentHourSilverSpent = currentHourSilverSpent,
                LifetimeSilverSpent = model.LifetimeSilverSpent,
                PendingPayment = model.PendingPayment
            };

            snapshot.HourlySilverHistory.AddRange(hourlySilverHistory);

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
                float silverPerSecond = effectiveTps * (Props.pricePerKTokens / 1000f);

                SubscriptionClawSnapshot clawSnapshot = new SubscriptionClawSnapshot
                {
                    Claw = claw,
                    CurrentTps = currentTps,
                    ProvidedTps = effectiveTps,
                    SilverPerSecond = silverPerSecond
                };

                if (clawSilverHistory.TryGetValue(claw.thingIDNumber, out List<float> history) && history != null)
                {
                    clawSnapshot.SilverPerSecondHistory.AddRange(history);
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
        }

        public override string CompInspectStringExtra()
        {
            float supplied = Mathf.Min(liveThroughput, totalTokenCapacityPerSecond);
            return "RimClaw_Subscription_Inspect".Translate(GetCurrentModelIdentifier(), liveThroughput.ToString("0.0"), totalTokenCapacityPerSecond.ToString("0.0"), connectedClawfish.Count, model.PerSecondRate.ToString("0.00"), currentHourSilverSpent.ToString("0.00"), model.PendingPayment.ToString("0.00"), supplied.ToString("0.0"));
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

        private void UpdateGlowerColor()
        {
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            CompGlower glower = parent.TryGetComp<CompGlower>();
            if (glower == null)
            {
                return;
            }

            Color color = RimClawGlowUtility.SoftenToGlow(ResolveModelColor());
            ColorInt colorInt = new ColorInt(
                Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                0);

            if (glower.GlowColor != colorInt)
            {
                glower.GlowColor = colorInt;
                glower.ForceRegister(parent.MapHeld);
            }
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
                return connection.GetAdjustedTokenConsumptionRate(claw);
            }

            CompClawfishToken legacy = claw.TryGetComp<CompClawfishToken>();
            if (legacy != null)
            {
                return legacy.GetCurrentConsumptionRatePerSecond(claw);
            }

            return 0f;
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
                float silverPerSecond = effectiveTps * (Props.pricePerKTokens / 1000f);

                if (!clawSilverHistory.TryGetValue(claw.thingIDNumber, out List<float> history) || history == null)
                {
                    history = new List<float>();
                    clawSilverHistory[claw.thingIDNumber] = history;
                }

                history.Add(silverPerSecond);
                if (history.Count > maxLen)
                {
                    history.RemoveAt(0);
                }
            }
        }

        private void TickHourlyHistory(float elapsedSilver, float elapsedSeconds)
        {
            currentHourSilverSpent += elapsedSilver;
            currentHourElapsedGameSeconds += elapsedSeconds;

            while (currentHourElapsedGameSeconds >= InGameSecondsPerHour)
            {
                hourlySilverHistory.Add(currentHourSilverSpent);
                int maxLen = Mathf.Max(24, Props.historyLength);
                if (hourlySilverHistory.Count > maxLen)
                {
                    hourlySilverHistory.RemoveAt(0);
                }

                currentHourElapsedGameSeconds -= InGameSecondsPerHour;
                currentHourSilverSpent = 0f;
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

        private bool TryAutoPayIntegerSilver(bool stopOnFailure)
        {
            if (parent.MapHeld == null)
            {
                return false;
            }

            while (model.PendingPayment >= 1f)
            {
                if (!ConsumeNearbySilverUnit())
                {
                    if (stopOnFailure)
                    {
                        StopServiceForNonPayment();
                    }

                    return false;
                }

                model.PendingPayment = Mathf.Max(0f, model.PendingPayment - 1f);
            }

            return true;
        }

        private bool ConsumeNearbySilverUnit()
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.MapHeld, Props.silverSearchRadius, useCenter: true))
            {
                if (thing.def != ThingDefOf.Silver)
                {
                    continue;
                }

                if (thing.stackCount <= 0)
                {
                    continue;
                }

                thing.SplitOff(1).Destroy(DestroyMode.Vanish);
                return true;
            }

            return false;
        }

        private void StopServiceForNonPayment()
        {
            if (serviceStopped)
            {
                return;
            }

            serviceStopped = true;
            liveThroughput = 0f;
            currentPerSecondSilverRate = 0f;
            model.PerSecondRate = 0f;
            model.PendingPayment = Mathf.Max(0f, model.PendingPayment);
            Messages.Message("RimClaw_Subscription_OutOfFee".Translate(), parent, MessageTypeDefOf.CautionInput, historical: false);
        }

        private void ResumeServiceAfterPayment()
        {
            if (!serviceStopped)
            {
                return;
            }

            serviceStopped = false;
            Messages.Message("RimClaw_Subscription_Resumed".Translate(), parent, MessageTypeDefOf.PositiveEvent, historical: false);
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
