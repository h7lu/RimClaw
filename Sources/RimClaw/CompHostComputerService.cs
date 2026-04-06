using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_HostComputerService : CompProperties
    {
        public int connectionRadius = 9;

        public CompProperties_HostComputerService()
        {
            compClass = typeof(CompHostComputerService);
        }
    }

    public class HostGpuSnapshot
    {
        public int ThingId;
        public string Name;
        public string ModelName;
        public Color ModelColor;
        public int TotalVram;
        public int UsedVram;
        public int TotalInstances;
        public int UsedInstances;
        public float PerInstanceCapacityTps;
        public float CapacityTps;
        public float UsedTps;
        public float HeatRate;
        public float WorkSpeedMultiplier;
        public float TotalUsageFraction;
        public List<Pawn> AssignedClaws = new List<Pawn>();
        public List<float> InstanceUsageFractions = new List<float>();
    }

    public class HostComputerSnapshot
    {
        public int ActiveGpuCount;
        public int TotalGpuCount;
        public float RoomTemperature;
        public float TotalLiveTokenRate;
        public float TotalPowerConsumption;
        public int TotalConnectedClaws;
        public int UsedVram;
        public int TotalVram;
        public int ModelCount;
        public int UsedInstances;
        public int TotalInstances;
        public string ModelName;
        public float WorkSpeedMultiplier;
        public float HeatRate;
        public float ActiveTimeSeconds;
        public List<HostGpuSnapshot> Gpus = new List<HostGpuSnapshot>();
        public List<float> TpsHistory = new List<float>();
        public List<float> HeatHistory = new List<float>();
        public List<Pawn> ConnectedClaws = new List<Pawn>();
    }

    [StaticConstructorOnStartup]
    public class CompHostComputerService : ThingComp
    {
        private static readonly Material GrayLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.70f, 0.74f, 0.82f, 1f));
        private static readonly Material YellowLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1.00f, 0.86f, 0.24f, 1f));

        private List<Pawn> connectedClaws = new List<Pawn>();
        private readonly Dictionary<int, int> clawToGpuThing = new Dictionary<int, int>();
        private readonly List<Thing> connectedGpus = new List<Thing>();
        private readonly List<HostGpuSnapshot> gpuSnapshots = new List<HostGpuSnapshot>();
        private readonly List<float> tpsHistory = new List<float>();
        private readonly List<float> heatHistory = new List<float>();

        private List<int> assignmentPawnIds;
        private List<int> assignmentGpuIds;

        private Thing connectedMemoryDisk;
        private string modelName = "(none)";
        private float modelWorkSpeedMultiplier = 1f;
        private Color modelColor = Color.white;

        private int totalVram;
        private int usedVram;
        private int totalInstances;
        private int usedInstances;
        private float totalTokenCapacity;
        private float totalUsedTokenRate;
        private float totalHeatRate;
        private float totalPowerConsumption;
        private float activeTimeSeconds;

        private int lastRefreshTick = -999999;
        private int lastHistoryTick = -999999;

        public CompProperties_HostComputerService Props => (CompProperties_HostComputerService)props;
        public bool IsValidSupplier => parent != null && parent.Spawned && IsPowered(parent as ThingWithComps);

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref connectedClaws, "connectedClaws", LookMode.Reference);
            Scribe_Collections.Look(ref assignmentPawnIds, "assignmentPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref assignmentGpuIds, "assignmentGpuIds", LookMode.Value);
            Scribe_Values.Look(ref activeTimeSeconds, "activeTimeSeconds", 0f);
            Scribe_Values.Look(ref modelName, "modelName", "(none)");
            Scribe_Values.Look(ref modelWorkSpeedMultiplier, "modelWorkSpeedMultiplier", 1f);
            Scribe_Values.Look(ref modelColor, "modelColor", Color.white);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (connectedClaws == null)
                {
                    connectedClaws = new List<Pawn>();
                }

                clawToGpuThing.Clear();
                if (assignmentPawnIds != null && assignmentGpuIds != null)
                {
                    int count = Math.Min(assignmentPawnIds.Count, assignmentGpuIds.Count);
                    for (int i = 0; i < count; i++)
                    {
                        clawToGpuThing[assignmentPawnIds[i]] = assignmentGpuIds[i];
                    }
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(60))
            {
                return;
            }

            RefreshSnapshot(force: false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "Open Control Panel",
                defaultDesc = "Open the datacenter management console.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/TogglePower", reportFailure: false),
                action = delegate
                {
                    Find.WindowStack.Add(new Window_HostComputerControlPanel(this));
                }
            };
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(parent.Position, Props.connectionRadius);
            RefreshSnapshot(force: false);

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
            RefreshSnapshot(force: false);
            return $"Model: {modelName}\nCurrent TPS: {totalUsedTokenRate:0.0}/{totalTokenCapacity:0.0} needed\nConnected Clawfish: {connectedClaws.Count}\nConnected GPU: {connectedGpus.Count}\nVRAM: {usedVram}/{totalVram} GB";
        }

        public void AddConnectedClaw(Pawn claw)
        {
            if (claw == null || claw.Destroyed || claw.Dead)
            {
                return;
            }

            if (!connectedClaws.Contains(claw))
            {
                connectedClaws.Add(claw);
            }

            RefreshSnapshot(force: true);
            AutoAssignToBestGpu(claw);
        }

        public void RemoveConnectedClaw(Pawn claw)
        {
            if (claw == null)
            {
                return;
            }

            connectedClaws.Remove(claw);
            clawToGpuThing.Remove(claw.thingIDNumber);
        }

        public float GetAvailableTokenRateForClawfish(Pawn claw)
        {
            RefreshSnapshot(force: false);
            if (!IsValidSupplier || claw == null || !connectedClaws.Contains(claw))
            {
                return 0f;
            }

            if (gpuSnapshots.Count == 0)
            {
                return 0f;
            }

            if (!clawToGpuThing.TryGetValue(claw.thingIDNumber, out int gpuThingId) || !HasGpuSnapshot(gpuThingId))
            {
                AutoAssignToBestGpu(claw);
                clawToGpuThing.TryGetValue(claw.thingIDNumber, out gpuThingId);
            }

            HostGpuSnapshot assigned = GetGpuSnapshot(gpuThingId);
            if (assigned == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, assigned.PerInstanceCapacityTps);
        }

        public bool AssignClawToGpu(Pawn claw, int gpuThingId)
        {
            if (claw == null || !connectedClaws.Contains(claw) || !HasGpuSnapshot(gpuThingId))
            {
                return false;
            }

            HostGpuSnapshot snapshot = GetGpuSnapshot(gpuThingId);
            if (snapshot == null)
            {
                return false;
            }

            if (clawToGpuThing.TryGetValue(claw.thingIDNumber, out int existingGpuId) && existingGpuId == gpuThingId)
            {
                return true;
            }

            int currentAssignments = GetAssignedClawCountForGpu(gpuThingId);
            if (currentAssignments >= snapshot.TotalInstances)
            {
                return false;
            }

            clawToGpuThing[claw.thingIDNumber] = gpuThingId;
            return true;
        }

        public string GetAssignedGpuLabel(Pawn claw)
        {
            if (claw == null)
            {
                return "Unassigned";
            }

            if (!clawToGpuThing.TryGetValue(claw.thingIDNumber, out int gpuThingId))
            {
                return "Unassigned";
            }

            HostGpuSnapshot snapshot = GetGpuSnapshot(gpuThingId);
            return snapshot?.Name ?? "Unassigned";
        }

        public HostComputerSnapshot GetSnapshot()
        {
            RefreshSnapshot(force: false);
            HostComputerSnapshot snapshot = new HostComputerSnapshot
            {
                ActiveGpuCount = connectedGpus.Count,
                TotalGpuCount = connectedGpus.Count,
                RoomTemperature = parent.MapHeld == null ? 21f : parent.Position.GetTemperature(parent.MapHeld),
                TotalLiveTokenRate = totalUsedTokenRate,
                TotalPowerConsumption = totalPowerConsumption,
                TotalConnectedClaws = connectedClaws.Count,
                UsedVram = usedVram,
                TotalVram = totalVram,
                ModelCount = modelName == "(none)" ? 0 : 1,
                UsedInstances = usedInstances,
                TotalInstances = totalInstances,
                ModelName = modelName,
                WorkSpeedMultiplier = modelWorkSpeedMultiplier,
                HeatRate = totalHeatRate,
                ActiveTimeSeconds = activeTimeSeconds
            };

            snapshot.Gpus.AddRange(gpuSnapshots);
            snapshot.TpsHistory.AddRange(tpsHistory);
            snapshot.HeatHistory.AddRange(heatHistory);

            for (int i = 0; i < connectedClaws.Count; i++)
            {
                Pawn pawn = connectedClaws[i];
                if (pawn != null && !pawn.Destroyed && !pawn.Dead)
                {
                    snapshot.ConnectedClaws.Add(pawn);
                }
            }

            return snapshot;
        }

        private void RefreshSnapshot(bool force)
        {
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (!force && currentTick >= 0 && currentTick - lastRefreshTick < 60)
            {
                return;
            }

            int tickDelta = 0;
            if (currentTick >= 0)
            {
                if (lastRefreshTick < 0)
                {
                    lastRefreshTick = currentTick;
                    lastHistoryTick = currentTick;
                }
                else
                {
                    tickDelta = Mathf.Max(0, currentTick - lastRefreshTick);
                    lastRefreshTick = currentTick;
                }
            }

            CleanupConnectedClaws();
            ResolveHardwareNetwork();
            CalculateUsage();
            PersistAssignments();

            if (tickDelta > 0 && IsValidSupplier && modelName != "(none)" && connectedClaws.Count > 0)
            {
                activeTimeSeconds += tickDelta / 60f;
            }

            if (currentTick >= 0 && currentTick - lastHistoryTick >= 60)
            {
                PushHistory(totalUsedTokenRate, totalHeatRate);
                lastHistoryTick = currentTick;
            }
        }

        private void CleanupConnectedClaws()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int i = connectedClaws.Count - 1; i >= 0; i--)
            {
                Pawn pawn = connectedClaws[i];
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Map != parent.MapHeld || !seen.Add(pawn.thingIDNumber))
                {
                    connectedClaws.RemoveAt(i);
                    if (pawn != null)
                    {
                        clawToGpuThing.Remove(pawn.thingIDNumber);
                    }
                }
            }
        }

        private void ResolveHardwareNetwork()
        {
            connectedGpus.Clear();
            gpuSnapshots.Clear();
            connectedMemoryDisk = null;
            modelName = "(none)";
            modelWorkSpeedMultiplier = 1f;
            modelColor = Color.white;

            if (parent.MapHeld == null)
            {
                return;
            }

            CompMemoryDisk bestDisk = null;
            float bestDiskDist = float.MaxValue;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.MapHeld, Props.connectionRadius, useCenter: true))
            {
                CompGPUCluster gpu = thing.TryGetComp<CompGPUCluster>();
                if (gpu != null)
                {
                    gpu.AssignHost(parent);
                    connectedGpus.Add(thing);
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

            connectedGpus.Sort((a, b) =>
            {
                int zCompare = b.Position.z.CompareTo(a.Position.z);
                return zCompare != 0 ? zCompare : a.Position.x.CompareTo(b.Position.x);
            });

            int requiredVramPerInstance = 0;
            float tokenPerInstance = 0f;

            if (bestDisk != null && bestDisk.HasModel)
            {
                bestDisk.AssignHost(parent);
                modelName = bestDisk.ModelName;
                modelColor = bestDisk.ModelColor;
                modelWorkSpeedMultiplier = 1f + bestDisk.WorkSpeedBonus;
                requiredVramPerInstance = bestDisk.RequiredVram;
                tokenPerInstance = bestDisk.TokenPerSecondPerInstance;
            }
            else
            {
                connectedMemoryDisk = null;
            }

            for (int i = 0; i < connectedGpus.Count; i++)
            {
                Thing gpuThing = connectedGpus[i];
                CompGPUCluster gpuComp = gpuThing.TryGetComp<CompGPUCluster>();
                int gpuVram = gpuComp?.Props?.providedVRAM ?? 0;
                int gpuTotalInstances = requiredVramPerInstance > 0 ? gpuVram / requiredVramPerInstance : 0;
                float gpuCapacity = gpuTotalInstances * tokenPerInstance;

                gpuSnapshots.Add(new HostGpuSnapshot
                {
                    ThingId = gpuThing.thingIDNumber,
                    Name = $"GPU {i}",
                    ModelName = modelName,
                    ModelColor = modelColor,
                    TotalVram = gpuVram,
                    UsedVram = gpuTotalInstances * requiredVramPerInstance,
                    TotalInstances = gpuTotalInstances,
                    UsedInstances = 0,
                    PerInstanceCapacityTps = tokenPerInstance,
                    CapacityTps = gpuCapacity,
                    UsedTps = 0f,
                    HeatRate = gpuComp?.Props?.heatPerSecond ?? 0f,
                    WorkSpeedMultiplier = modelWorkSpeedMultiplier,
                    TotalUsageFraction = 0f
                });
            }
        }

        private void CalculateUsage()
        {
            totalVram = 0;
            usedVram = 0;
            totalInstances = 0;
            usedInstances = 0;
            totalTokenCapacity = 0f;
            totalUsedTokenRate = 0f;
            totalHeatRate = 0f;
            totalPowerConsumption = GetPowerConsumption(parent as ThingWithComps);

            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                HostGpuSnapshot gpu = gpuSnapshots[i];
                totalVram += gpu.TotalVram;
                usedVram += gpu.UsedVram;
                totalInstances += gpu.TotalInstances;
                totalTokenCapacity += gpu.CapacityTps;
                totalHeatRate += gpu.HeatRate;

                Thing gpuThing = FindThingById(gpu.ThingId);
                totalPowerConsumption += GetPowerConsumption(gpuThing as ThingWithComps);
            }

            if (connectedMemoryDisk != null)
            {
                totalPowerConsumption += GetPowerConsumption(connectedMemoryDisk as ThingWithComps);
            }

            for (int i = 0; i < connectedClaws.Count; i++)
            {
                Pawn claw = connectedClaws[i];
                if (claw == null)
                {
                    continue;
                }

                AutoAssignToBestGpu(claw);
                float demand = GetCurrentNeededRate(claw);
                if (!clawToGpuThing.TryGetValue(claw.thingIDNumber, out int gpuId))
                {
                    continue;
                }

                HostGpuSnapshot gpu = GetGpuSnapshot(gpuId);
                if (gpu == null || gpu.TotalInstances <= 0)
                {
                    continue;
                }

                float slotCap = Mathf.Max(0.001f, gpu.PerInstanceCapacityTps);
                float effective = Mathf.Min(demand, slotCap);
                totalUsedTokenRate += effective;

                gpu.AssignedClaws.Add(claw);
                gpu.InstanceUsageFractions.Add(Mathf.Clamp01(effective / slotCap));

                if (gpuId >= 0)
                {
                    gpu.UsedTps += effective;
                }
            }

            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                HostGpuSnapshot gpu = gpuSnapshots[i];
                gpu.UsedInstances = Mathf.Clamp(gpu.AssignedClaws.Count, 0, gpu.TotalInstances);
                gpu.TotalUsageFraction = gpu.CapacityTps <= 0f ? 0f : Mathf.Clamp01(gpu.UsedTps / Mathf.Max(0.001f, gpu.CapacityTps));
                usedInstances += gpu.UsedInstances;
            }
        }

        private void AutoAssignToBestGpu(Pawn claw)
        {
            if (claw == null || gpuSnapshots.Count == 0)
            {
                return;
            }

            if (clawToGpuThing.TryGetValue(claw.thingIDNumber, out int existingGpuId) && HasGpuSnapshot(existingGpuId))
            {
                return;
            }

            HostGpuSnapshot bestGpu = null;
            int bestFree = int.MinValue;

            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                HostGpuSnapshot gpu = gpuSnapshots[i];
                int free = gpu.TotalInstances - GetAssignedClawCountForGpu(gpu.ThingId);
                if (free > bestFree)
                {
                    bestFree = free;
                    bestGpu = gpu;
                }
            }

            if (bestGpu != null && bestFree > 0)
            {
                clawToGpuThing[claw.thingIDNumber] = bestGpu.ThingId;
            }
        }

        private int GetAssignedClawCountForGpu(int gpuThingId)
        {
            int count = 0;
            foreach (KeyValuePair<int, int> kvp in clawToGpuThing)
            {
                if (kvp.Value == gpuThingId)
                {
                    count++;
                }
            }

            return count;
        }

        private float GetCurrentNeededRate(Pawn pawn)
        {
            CompClawfishTokenConnection connection = pawn.TryGetComp<CompClawfishTokenConnection>();
            if (connection != null)
            {
                return connection.CurrentTokenConsumptionRate;
            }

            CompClawfishToken legacyToken = pawn.TryGetComp<CompClawfishToken>();
            if (legacyToken != null)
            {
                return legacyToken.GetCurrentConsumptionRatePerSecond(pawn);
            }

            return 0f;
        }

        private Thing FindThingById(int thingId)
        {
            if (parent.MapHeld == null)
            {
                return null;
            }

            List<Thing> allThings = parent.MapHeld.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                if (allThings[i].thingIDNumber == thingId)
                {
                    return allThings[i];
                }
            }

            return null;
        }

        private bool HasGpuSnapshot(int thingId)
        {
            return GetGpuSnapshot(thingId) != null;
        }

        private HostGpuSnapshot GetGpuSnapshot(int thingId)
        {
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                if (gpuSnapshots[i].ThingId == thingId)
                {
                    return gpuSnapshots[i];
                }
            }

            return null;
        }

        private void PersistAssignments()
        {
            assignmentPawnIds = new List<int>();
            assignmentGpuIds = new List<int>();

            foreach (KeyValuePair<int, int> kvp in clawToGpuThing)
            {
                assignmentPawnIds.Add(kvp.Key);
                assignmentGpuIds.Add(kvp.Value);
            }
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

        private static float GetPowerConsumption(ThingWithComps thing)
        {
            if (thing == null)
            {
                return 0f;
            }

            CompPowerTrader power = thing.GetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
            {
                return 0f;
            }

            return Mathf.Abs(power.PowerOutput);
        }

        private void PushHistory(float tpsValue, float heatValue)
        {
            tpsHistory.Add(tpsValue);
            heatHistory.Add(heatValue);

            const int maxPoints = 20000;
            if (tpsHistory.Count > maxPoints)
            {
                tpsHistory.RemoveAt(0);
            }

            if (heatHistory.Count > maxPoints)
            {
                heatHistory.RemoveAt(0);
            }
        }
    }
}
