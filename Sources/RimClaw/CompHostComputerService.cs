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
        public int ModelDiskThingId;
        public bool IsActive;
        public int TotalVram;
        public int UsedVram;
        public int TotalInstances;
        public int UsedInstances;
        public float PerInstanceCapacityTps;
        public float CapacityTps;
        public float UsedTps;
        public float HeatRate;
        public float BaseHeatPerSecond;
        public float HeatPerUsageFraction;
        public float HeatAverageSeconds;
        public float MaxTemperatureC;
        public float WorkSpeedMultiplier;
        public float TotalUsageFraction;
        public List<Pawn> AssignedClaws = new List<Pawn>();
        public List<float> InstanceUsageFractions = new List<float>();
        public List<float> HeatHistory = new List<float>();
        public List<float> TpsHistory = new List<float>();
    }

    public class HostModelOptionSnapshot
    {
        public int DiskThingId;
        public string ModelName;
        public Color ModelColor;
        public int RequiredVram;
        public float TokenPerSecondPerInstance;
        public float WorkSpeedBonus;
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
        public List<HostModelOptionSnapshot> AvailableModels = new List<HostModelOptionSnapshot>();
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
        private readonly Dictionary<int, int> gpuToModelDiskThing = new Dictionary<int, int>();
        private readonly List<Thing> connectedGpus = new List<Thing>();
        private readonly List<CompMemoryDisk> connectedMemoryDisks = new List<CompMemoryDisk>();
        private readonly List<HostGpuSnapshot> gpuSnapshots = new List<HostGpuSnapshot>();
        private readonly List<HostModelOptionSnapshot> availableModels = new List<HostModelOptionSnapshot>();
        private readonly List<float> tpsHistory = new List<float>();
        private readonly List<float> heatHistory = new List<float>();
        private readonly Dictionary<int, List<float>> gpuUsageHistory = new Dictionary<int, List<float>>();
        private readonly Dictionary<int, List<float>> gpuHeatHistory = new Dictionary<int, List<float>>();
        private readonly Dictionary<int, List<float>> gpuTpsHistory = new Dictionary<int, List<float>>();

        private List<int> assignmentPawnIds;
        private List<int> assignmentGpuIds;
        private List<int> gpuModelGpuIds;
        private List<int> gpuModelDiskIds;

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
            Scribe_Collections.Look(ref gpuModelGpuIds, "gpuModelGpuIds", LookMode.Value);
            Scribe_Collections.Look(ref gpuModelDiskIds, "gpuModelDiskIds", LookMode.Value);
            Scribe_Values.Look(ref activeTimeSeconds, "activeTimeSeconds", 0f);

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

                gpuToModelDiskThing.Clear();
                if (gpuModelGpuIds != null && gpuModelDiskIds != null)
                {
                    int count = Math.Min(gpuModelGpuIds.Count, gpuModelDiskIds.Count);
                    for (int i = 0; i < count; i++)
                    {
                        gpuToModelDiskThing[gpuModelGpuIds[i]] = gpuModelDiskIds[i];
                    }
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsPowered(parent as ThingWithComps) && parent.IsHashIntervalTick(30))
            {
                RimClawGlowUtility.SpawnPulseGlow(parent, new Color32(240, 255, 240, 255), 5f);
            }

            if (!parent.IsHashIntervalTick(60))
            {
                return;
            }

            RefreshSnapshot(force: false);
            PushGpuHeat();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            RefreshSnapshot(force: false);
            PushGpuHeat();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "Open Console",
                defaultDesc = "Open the datacenter management console.",
                icon = ContentFinder<Texture2D>.Get("control_panel", reportFailure: false),
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

            for (int i = 0; i < connectedMemoryDisks.Count; i++)
            {
                Thing diskThing = connectedMemoryDisks[i]?.parent;
                if (diskThing != null && diskThing.Spawned && diskThing.Map == parent.Map)
                {
                    GenDraw.DrawLineBetween(parent.DrawPos, diskThing.DrawPos, YellowLineMat);
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            RefreshSnapshot(force: false);
            int activeGpuCount = 0;
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                if (gpuSnapshots[i].IsActive)
                {
                    activeGpuCount++;
                }
            }

            return $"Models connected: {availableModels.Count}\nCurrent TPS: {totalUsedTokenRate:0.0}/{totalTokenCapacity:0.0} needed\nConnected Clawfish: {connectedClaws.Count}\nConnected GPU: {activeGpuCount}/{connectedGpus.Count}\nVRAM: {usedVram}/{totalVram} GB";
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent?.Spawned != true || !IsPowered(parent as ThingWithComps))
            {
                return;
            }

            RimClawGlowUtility.DrawGlow(parent.DrawPos, new Color32(240, 255, 240, 255), 5f);
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
            if (assigned == null || !assigned.IsActive)
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

            if (!snapshot.IsActive)
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
                return "None";
            }

            if (!clawToGpuThing.TryGetValue(claw.thingIDNumber, out int gpuThingId))
            {
                return "None";
            }

            HostGpuSnapshot snapshot = GetGpuSnapshot(gpuThingId);
            if (snapshot == null || !snapshot.IsActive)
            {
                return "None";
            }

            return snapshot.Name;
        }

        public bool SetGpuModelForGpu(int gpuThingId, int diskThingId)
        {
            RefreshSnapshot(force: true);
            HostGpuSnapshot gpu = GetGpuSnapshot(gpuThingId);
            if (gpu == null)
            {
                return false;
            }

            int normalizedDiskId = diskThingId;
            if (normalizedDiskId >= 0 && GetModelOption(normalizedDiskId) == null)
            {
                return false;
            }

            if (gpuToModelDiskThing.TryGetValue(gpuThingId, out int existingDiskId) && existingDiskId == normalizedDiskId)
            {
                return true;
            }

            gpuToModelDiskThing[gpuThingId] = normalizedDiskId;
            int unassigned = RemoveAssignmentsForGpu(gpuThingId);
            RefreshSnapshot(force: true);

            if (unassigned > 0)
            {
                Messages.Message("Clawfish on that GPU were deassigned. Reassign them to active instances.", parent, MessageTypeDefOf.CautionInput, historical: false);
            }

            return true;
        }

        public HostComputerSnapshot GetSnapshot()
        {
            RefreshSnapshot(force: false);
            int activeGpuCount = 0;
            string firstModelName = "(none)";
            float firstWorkSpeedMultiplier = 1f;
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                if (!gpuSnapshots[i].IsActive)
                {
                    continue;
                }

                activeGpuCount++;
                if (firstModelName == "(none)")
                {
                    firstModelName = gpuSnapshots[i].ModelName;
                    firstWorkSpeedMultiplier = gpuSnapshots[i].WorkSpeedMultiplier;
                }
            }

            HostComputerSnapshot snapshot = new HostComputerSnapshot
            {
                ActiveGpuCount = activeGpuCount,
                TotalGpuCount = connectedGpus.Count,
                RoomTemperature = parent.MapHeld == null ? 21f : parent.Position.GetTemperature(parent.MapHeld),
                TotalLiveTokenRate = totalUsedTokenRate,
                TotalPowerConsumption = totalPowerConsumption,
                TotalConnectedClaws = connectedClaws.Count,
                UsedVram = usedVram,
                TotalVram = totalVram,
                ModelCount = availableModels.Count,
                UsedInstances = usedInstances,
                TotalInstances = totalInstances,
                ModelName = firstModelName,
                WorkSpeedMultiplier = firstWorkSpeedMultiplier,
                HeatRate = totalHeatRate,
                ActiveTimeSeconds = activeTimeSeconds
            };

            snapshot.Gpus.AddRange(gpuSnapshots);
            snapshot.AvailableModels.AddRange(availableModels);
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

            if (tickDelta > 0 && IsValidSupplier && connectedClaws.Count > 0 && availableModels.Count > 0)
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
            connectedMemoryDisks.Clear();
            gpuSnapshots.Clear();
            availableModels.Clear();

            if (parent.MapHeld == null)
            {
                return;
            }

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
                    disk.AssignHost(parent);
                    connectedMemoryDisks.Add(disk);
                }
            }

            connectedGpus.Sort((a, b) =>
            {
                int zCompare = b.Position.z.CompareTo(a.Position.z);
                return zCompare != 0 ? zCompare : a.Position.x.CompareTo(b.Position.x);
            });

            for (int i = 0; i < connectedMemoryDisks.Count; i++)
            {
                CompMemoryDisk disk = connectedMemoryDisks[i];
                if (disk == null || !disk.HasModel)
                {
                    continue;
                }

                availableModels.Add(new HostModelOptionSnapshot
                {
                    DiskThingId = disk.parent.thingIDNumber,
                    ModelName = disk.ModelName,
                    ModelColor = disk.ModelColor,
                    RequiredVram = disk.RequiredVram,
                    TokenPerSecondPerInstance = disk.TokenPerSecondPerInstance,
                    WorkSpeedBonus = disk.WorkSpeedBonus
                });
            }

            for (int i = 0; i < connectedGpus.Count; i++)
            {
                Thing gpuThing = connectedGpus[i];
                CompGPUCluster gpuComp = gpuThing.TryGetComp<CompGPUCluster>();
                int gpuVram = gpuComp?.Props?.providedVRAM ?? 0;

                int selectedDiskId;
                if (!gpuToModelDiskThing.TryGetValue(gpuThing.thingIDNumber, out selectedDiskId))
                {
                    selectedDiskId = availableModels.Count > 0 ? availableModels[0].DiskThingId : -1;
                    gpuToModelDiskThing[gpuThing.thingIDNumber] = selectedDiskId;
                }

                HostModelOptionSnapshot selectedModel = GetModelOption(selectedDiskId);
                bool isActive = selectedModel != null;
                int requiredVramPerInstance = isActive ? Mathf.Max(0, selectedModel.RequiredVram) : 0;
                float tokenPerInstance = isActive ? Mathf.Max(0f, selectedModel.TokenPerSecondPerInstance) : 0f;
                int gpuTotalInstances = requiredVramPerInstance > 0 ? gpuVram / requiredVramPerInstance : 0;
                float gpuCapacity = gpuTotalInstances * tokenPerInstance;
                Color gpuColor = isActive ? selectedModel.ModelColor : new Color32(100, 100, 100, 255);
                string gpuModelName = isActive ? selectedModel.ModelName : "(None)";
                float workSpeedMultiplier = isActive ? 1f + selectedModel.WorkSpeedBonus : 1f;

                gpuSnapshots.Add(new HostGpuSnapshot
                {
                    ThingId = gpuThing.thingIDNumber,
                    Name = $"GPU {i}",
                    ModelName = gpuModelName,
                    ModelColor = gpuColor,
                    ModelDiskThingId = isActive ? selectedModel.DiskThingId : -1,
                    IsActive = isActive,
                    TotalVram = gpuVram,
                    UsedVram = gpuTotalInstances * requiredVramPerInstance,
                    TotalInstances = gpuTotalInstances,
                    UsedInstances = 0,
                    PerInstanceCapacityTps = tokenPerInstance,
                    CapacityTps = gpuCapacity,
                    UsedTps = 0f,
                    HeatRate = 0f,
                    BaseHeatPerSecond = gpuComp?.Props?.baseHeatPerSecond ?? 5f,
                    HeatPerUsageFraction = gpuComp?.Props?.heatPerUsageFraction ?? 20f,
                    HeatAverageSeconds = Mathf.Max(1f, gpuComp?.Props?.heatAverageSeconds ?? 15f),
                    MaxTemperatureC = gpuComp?.Props?.maxTemperatureC ?? 1000f,
                    WorkSpeedMultiplier = workSpeedMultiplier,
                    TotalUsageFraction = 0f
                });
            }

            CleanupInactiveAssignments();
            CleanupStaleModelAssignments();
            CleanupHeatHistory();
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

                Thing gpuThing = FindThingById(gpu.ThingId);
                totalPowerConsumption += GetPowerConsumption(gpuThing as ThingWithComps);
            }

            for (int i = 0; i < connectedMemoryDisks.Count; i++)
            {
                totalPowerConsumption += GetPowerConsumption(connectedMemoryDisks[i]?.parent as ThingWithComps);
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
                if (gpu == null || !gpu.IsActive || gpu.TotalInstances <= 0)
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

                float usageAverage = UpdateGpuUsageHistory(gpu.ThingId, gpu.TotalUsageFraction, Mathf.RoundToInt(gpu.HeatAverageSeconds));
                float currentHeatRate = gpu.IsActive ? gpu.BaseHeatPerSecond + usageAverage * gpu.HeatPerUsageFraction : 0f;
                gpu.HeatRate = currentHeatRate;
                gpu.HeatHistory = UpdateGpuHeatHistory(gpu.ThingId, currentHeatRate);
                gpu.TpsHistory = UpdateGpuTpsHistory(gpu.ThingId, gpu.UsedTps);
                totalHeatRate += currentHeatRate;
                usedInstances += gpu.UsedInstances;
            }
        }

        private void PushGpuHeat()
        {
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                HostGpuSnapshot gpu = gpuSnapshots[i];
                if (!gpu.IsActive || gpu.HeatRate <= 0f)
                {
                    continue;
                }

                Thing gpuThing = FindThingById(gpu.ThingId);
                if (gpuThing == null || gpuThing.Map != parent.MapHeld)
                {
                    continue;
                }

                float ambientTemperature = gpuThing.AmbientTemperature;
                if (ambientTemperature >= gpu.MaxTemperatureC)
                {
                    continue;
                }

                GenTemperature.PushHeat(gpuThing.Position, gpuThing.Map, gpu.HeatRate);
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
                if (!gpu.IsActive)
                {
                    continue;
                }

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

        private HostModelOptionSnapshot GetModelOption(int diskThingId)
        {
            for (int i = 0; i < availableModels.Count; i++)
            {
                if (availableModels[i].DiskThingId == diskThingId)
                {
                    return availableModels[i];
                }
            }

            return null;
        }

        private int RemoveAssignmentsForGpu(int gpuThingId)
        {
            List<int> toRemove = new List<int>();
            foreach (KeyValuePair<int, int> kvp in clawToGpuThing)
            {
                if (kvp.Value == gpuThingId)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                clawToGpuThing.Remove(toRemove[i]);
            }

            return toRemove.Count;
        }

        private void CleanupInactiveAssignments()
        {
            HashSet<int> activeGpuIds = new HashSet<int>();
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                if (gpuSnapshots[i].IsActive)
                {
                    activeGpuIds.Add(gpuSnapshots[i].ThingId);
                }
            }

            List<int> toRemove = new List<int>();
            foreach (KeyValuePair<int, int> kvp in clawToGpuThing)
            {
                if (!activeGpuIds.Contains(kvp.Value))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                clawToGpuThing.Remove(toRemove[i]);
            }
        }

        private void CleanupStaleModelAssignments()
        {
            HashSet<int> validGpuIds = new HashSet<int>();
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                validGpuIds.Add(gpuSnapshots[i].ThingId);
            }

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, int> kvp in gpuToModelDiskThing)
            {
                if (!validGpuIds.Contains(kvp.Key))
                {
                    stale.Add(kvp.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                gpuToModelDiskThing.Remove(stale[i]);
            }
        }

        private void CleanupHeatHistory()
        {
            HashSet<int> validGpuIds = new HashSet<int>();
            for (int i = 0; i < gpuSnapshots.Count; i++)
            {
                validGpuIds.Add(gpuSnapshots[i].ThingId);
            }

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, List<float>> kvp in gpuUsageHistory)
            {
                if (!validGpuIds.Contains(kvp.Key))
                {
                    stale.Add(kvp.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                gpuUsageHistory.Remove(stale[i]);
            }

            stale.Clear();
            foreach (KeyValuePair<int, List<float>> kvp in gpuHeatHistory)
            {
                if (!validGpuIds.Contains(kvp.Key))
                {
                    stale.Add(kvp.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                gpuHeatHistory.Remove(stale[i]);
            }

            stale.Clear();
            foreach (KeyValuePair<int, List<float>> kvp in gpuTpsHistory)
            {
                if (!validGpuIds.Contains(kvp.Key))
                {
                    stale.Add(kvp.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                gpuTpsHistory.Remove(stale[i]);
            }
        }

        private float UpdateGpuUsageHistory(int gpuThingId, float usageFraction, int windowSamples)
        {
            if (!gpuUsageHistory.TryGetValue(gpuThingId, out List<float> history))
            {
                history = new List<float>();
                gpuUsageHistory[gpuThingId] = history;
            }

            history.Add(Mathf.Clamp01(usageFraction));
            int excess = history.Count - Mathf.Max(1, windowSamples);
            if (excess > 0)
            {
                history.RemoveRange(0, excess);
            }

            float sum = 0f;
            for (int i = 0; i < history.Count; i++)
            {
                sum += history[i];
            }

            return history.Count > 0 ? sum / history.Count : 0f;
        }

        private List<float> UpdateGpuHeatHistory(int gpuThingId, float heatValue)
        {
            if (!gpuHeatHistory.TryGetValue(gpuThingId, out List<float> history))
            {
                history = new List<float>();
                gpuHeatHistory[gpuThingId] = history;
            }

            history.Add(heatValue);
            const int maxPoints = 20000;
            if (history.Count > maxPoints)
            {
                history.RemoveAt(0);
            }

            return history;
        }

        private List<float> UpdateGpuTpsHistory(int gpuThingId, float tpsValue)
        {
            if (!gpuTpsHistory.TryGetValue(gpuThingId, out List<float> history))
            {
                history = new List<float>();
                gpuTpsHistory[gpuThingId] = history;
            }

            history.Add(tpsValue);
            const int maxPoints = 20000;
            if (history.Count > maxPoints)
            {
                history.RemoveAt(0);
            }

            return history;
        }

        private void PersistAssignments()
        {
            assignmentPawnIds = new List<int>();
            assignmentGpuIds = new List<int>();
            gpuModelGpuIds = new List<int>();
            gpuModelDiskIds = new List<int>();

            foreach (KeyValuePair<int, int> kvp in clawToGpuThing)
            {
                assignmentPawnIds.Add(kvp.Key);
                assignmentGpuIds.Add(kvp.Value);
            }

            foreach (KeyValuePair<int, int> kvp in gpuToModelDiskThing)
            {
                gpuModelGpuIds.Add(kvp.Key);
                gpuModelDiskIds.Add(kvp.Value);
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
