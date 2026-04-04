using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [StaticConstructorOnStartup]
    public abstract class CompClawfishConnectorBase : ThingComp
    {
        protected List<Pawn> connected = new List<Pawn>();
        protected static readonly Material GrayLineMat;
        protected static readonly Material YellowLineMat;

        static CompClawfishConnectorBase()
        {
            GrayLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.70f, 0.74f, 0.82f, 1f));
            YellowLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1.00f, 0.86f, 0.24f, 1f));
        }

        protected abstract float ConnectionRadius { get; }
        protected abstract string ConnectorLabel { get; }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref connected, "connectedClawfish", LookMode.Reference);
            if (connected == null)
            {
                connected = new List<Pawn>();
            }
        }

        protected void CleanupConnections()
        {
            for (int i = connected.Count - 1; i >= 0; i--)
            {
                Pawn pawn = connected[i];
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Map != parent.Map)
                {
                    connected.RemoveAt(i);
                }
            }
        }

        protected void ConnectNearbyClawfish()
        {
            int added = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, ConnectionRadius, useCenter: true))
            {
                if (thing is Pawn pawn && ClawfishUtility.IsClawfish(pawn) && !connected.Contains(pawn))
                {
                    connected.Add(pawn);
                    added++;
                }
            }

            Messages.Message($"{ConnectorLabel}: connected {added} clawfish.", parent, MessageTypeDefOf.TaskCompletion, historical: false);
        }

        protected void DisconnectAll()
        {
            connected.Clear();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.MapHeld == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "Connect nearby Clawfish",
                defaultDesc = "Connect all Clawfish in radius.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", reportFailure: false),
                action = ConnectNearbyClawfish
            };
            yield return new Command_Action
            {
                defaultLabel = "Disconnect all",
                defaultDesc = "Clear all connected Clawfish links.",
                action = DisconnectAll
            };
        }

        protected static CompClawfishToken TokenComp(Pawn pawn)
        {
            return pawn?.TryGetComp<CompClawfishToken>();
        }

        protected static bool IsPowered(ThingWithComps thing)
        {
            CompPowerTrader power = thing.GetComp<CompPowerTrader>();
            return power == null || power.PowerOn;
        }
    }

    public class CompProperties_LLMServiceSubscription : CompProperties
    {
        public float tokenSupplyPerSecond = 20f;
        public float connectionRadius = 80f;
        public string majorCrashStatusText = "Errorcode 429";

        public CompProperties_LLMServiceSubscription()
        {
            compClass = typeof(CompLLMServiceSubscription);
        }
    }

    public class CompLLMServiceSubscription : CompClawfishConnectorBase
    {
        private int lastCrashTick = -999999;

        public CompProperties_LLMServiceSubscription Props => (CompProperties_LLMServiceSubscription)props;
        protected override float ConnectionRadius => Props.connectionRadius;
        protected override string ConnectorLabel => "LLM Service Subscription";

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || !parent.IsHashIntervalTick(60))
            {
                return;
            }

            CleanupConnections();
            if (!IsPowered(parent as ThingWithComps))
            {
                return;
            }

            CompModelCard model = parent.TryGetComp<CompModelCard>();
            model?.EnsureInitialized();
            float supplyPerSecond = model?.TokenPerSecondPerInstance ?? Props.tokenSupplyPerSecond;

            float demand = 0f;
            for (int i = 0; i < connected.Count; i++)
            {
                CompClawfishToken token = TokenComp(connected[i]);
                if (token != null)
                {
                    demand += token.GetCurrentConsumptionRatePerSecond(connected[i]);
                }
            }

            if (demand > supplyPerSecond)
            {
                MajorCrash();
                return;
            }

            if (demand <= 0.0001f)
            {
                return;
            }

            for (int i = 0; i < connected.Count; i++)
            {
                Pawn pawn = connected[i];
                CompClawfishToken token = TokenComp(pawn);
                if (token == null)
                {
                    continue;
                }

                float share = token.GetCurrentConsumptionRatePerSecond(pawn) / demand;
                token.AddTokens(supplyPerSecond * share);
                ClawfishUtility.EnsureServiceBoost(pawn, 0.05f);
            }
        }

        private void MajorCrash()
        {
            for (int i = 0; i < connected.Count; i++)
            {
                CompClawfishToken token = TokenComp(connected[i]);
                token?.ForceDepleted();
            }

            if (Find.TickManager.TicksGame - lastCrashTick > 1200)
            {
                lastCrashTick = Find.TickManager.TicksGame;
                Find.LetterStack.ReceiveLetter(
                    "Major crash: Errorcode 429",
                    "Connected Clawfish demand exceeded service throughput. All linked Clawfish have crashed.",
                    LetterDefOf.NegativeEvent,
                    parent);
            }
        }

        public override string CompInspectStringExtra()
        {
            CleanupConnections();
            float demand = 0f;
            for (int i = 0; i < connected.Count; i++)
            {
                CompClawfishToken token = TokenComp(connected[i]);
                if (token != null)
                {
                    demand += token.GetCurrentConsumptionRatePerSecond(connected[i]);
                }
            }

            CompModelCard model = parent.TryGetComp<CompModelCard>();
            model?.EnsureInitialized();
            float supplyPerSecond = model?.TokenPerSecondPerInstance ?? Props.tokenSupplyPerSecond;

            string status = demand > supplyPerSecond ? Props.majorCrashStatusText : "Online";
            string modelLine = model == null ? string.Empty : $"\nModel: {model.ModelName}";
            return $"Connected: {connected.Count}\nSupply: {supplyPerSecond:0.0} tok/s\nDemand: {demand:0.0} tok/s{modelLine}\nStatus: {status}";
        }
    }

    public class CompProperties_HostComputer : CompProperties
    {
        public int connectionRadius = 9;

        public CompProperties_HostComputer()
        {
            compClass = typeof(CompHostComputer);
        }
    }

    public class CompHostComputer : CompClawfishConnectorBase
    {
        private readonly List<Thing> connectedGpus = new List<Thing>();
        private Thing connectedMemoryDisk;
        private int gpuCount;
        private int totalVram;
        private int usedVram;
        private int instances;
        private float totalTokenSupply;
        private string modelName;

        public CompProperties_HostComputer Props => (CompProperties_HostComputer)props;
        protected override float ConnectionRadius => Props.connectionRadius;
        protected override string ConnectorLabel => "Host Computer";

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || !parent.IsHashIntervalTick(60))
            {
                return;
            }

            CleanupConnections();
            ResolveNetwork();

            if (!IsPowered(parent as ThingWithComps) || totalTokenSupply <= 0.0001f)
            {
                return;
            }

            float demand = 0f;
            for (int i = 0; i < connected.Count; i++)
            {
                CompClawfishToken token = TokenComp(connected[i]);
                if (token != null)
                {
                    demand += token.GetCurrentConsumptionRatePerSecond(connected[i]);
                }
            }

            if (demand <= 0.0001f)
            {
                return;
            }

            float distributable = Mathf.Min(demand, totalTokenSupply);
            for (int i = 0; i < connected.Count; i++)
            {
                Pawn pawn = connected[i];
                CompClawfishToken token = TokenComp(pawn);
                if (token == null)
                {
                    continue;
                }

                float share = token.GetCurrentConsumptionRatePerSecond(pawn) / demand;
                token.AddTokens(distributable * share);
                ClawfishUtility.EnsureServiceBoost(pawn, 0.01f * instances);
            }
        }

        private void ResolveNetwork()
        {
            connectedGpus.Clear();
            connectedMemoryDisk = null;
            gpuCount = 0;
            totalVram = 0;
            usedVram = 0;
            instances = 0;
            totalTokenSupply = 0f;
            modelName = "(none)";

            CompMemoryDisk bestDisk = null;
            float bestDiskDist = float.MaxValue;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.connectionRadius, useCenter: true))
            {
                CompGPUCluster gpu = thing.TryGetComp<CompGPUCluster>();
                if (gpu != null)
                {
                    gpu.AssignHost(parent);
                    connectedGpus.Add(thing);
                    gpuCount++;
                    totalVram += gpu.Props.providedVRAM;

                    continue;
                }

                CompMemoryDisk disk = thing.TryGetComp<CompMemoryDisk>();
                if (disk != null)
                {
                    float dist = thing.Position.DistanceTo(parent.Position);
                    if (dist < bestDiskDist)
                    {
                        bestDiskDist = dist;
                        bestDisk = disk;
                        connectedMemoryDisk = thing;
                    }
                }
            }

            if (bestDisk == null || !bestDisk.HasModel)
            {
                connectedMemoryDisk = null;
                return;
            }

            bestDisk.AssignHost(parent);

            modelName = bestDisk.ModelName;
            if (bestDisk.RequiredVram <= 0)
            {
                return;
            }

            instances = totalVram / bestDisk.RequiredVram;
            usedVram = instances * bestDisk.RequiredVram;
            totalTokenSupply = instances * bestDisk.TokenPerSecondPerInstance;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(parent.Position, Props.connectionRadius);
            if (!IsPowered(parent as ThingWithComps))
            {
                return;
            }

            ResolveNetwork();

            for (int i = 0; i < connectedGpus.Count; i++)
            {
                Thing gpu = connectedGpus[i];
                if (gpu?.Spawned == true && gpu.Map == parent.Map)
                {
                    GenDraw.DrawLineBetween(parent.DrawPos, gpu.DrawPos, GrayLineMat);
                }
            }

            if (connectedMemoryDisk != null && connectedMemoryDisk.Spawned && connectedMemoryDisk.Map == parent.Map)
            {
                GenDraw.DrawLineBetween(parent.DrawPos, connectedMemoryDisk.DrawPos, YellowLineMat);
            }
        }

        public override string CompInspectStringExtra()
        {
            return $"Model: {modelName}\nVRAM: {usedVram}/{totalVram} GB ({instances} instances)\nTotal token supply: {totalTokenSupply:0} tok/s\nConnected Clawfish: {connected.Count}\nConnected GPU: {gpuCount}";
        }
    }
}
