using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public static class GpuBreakdownRiskUtility
    {
        public const float MaxBreakChancePerSecond = 0.005f;
        public const float WarningThresholdPerSecond = 0.001f;
        public const int BreakdownCheckIntervalTicks = 1041;

        public static float ComputeBreakChancePerSecond(Thing gpuThing)
        {
            if (gpuThing == null || !gpuThing.Spawned || gpuThing.Map == null)
            {
                return MaxBreakChancePerSecond;
            }

            Room room = gpuThing.GetRoom();
            bool noRoom = room == null || !room.ProperRoom;
            if (noRoom)
            {
                return MaxBreakChancePerSecond;
            }

            float temperatureC = room.Temperature;
            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            float openness = room.CellCount > 0 ? (float)room.OpenRoofCount / room.CellCount : 1f;

            float temperatureRisk = Mathf.Clamp01((temperatureC - 35f) / 85f);
            float cleanlinessRisk = Mathf.Clamp01((0f - cleanliness) / 5f);
            float opennessRisk = Mathf.Clamp01(openness / 0.5f);

            bool roofed = gpuThing.Map.roofGrid.Roofed(gpuThing.Position);
            bool rainingOnTile = !roofed && gpuThing.Map.weatherManager.RainRate > 0f;
            float rainRisk = rainingOnTile ? 1f : 0f;

            float risk = Mathf.Max(temperatureRisk, cleanlinessRisk, opennessRisk, rainRisk);
            return MaxBreakChancePerSecond * risk;
        }

        public static float ComputeBreakChancePerCheck(float breakChancePerSecond)
        {
            float p = Mathf.Clamp01(breakChancePerSecond);
            float intervalSeconds = BreakdownCheckIntervalTicks / 60f;
            return 1f - Mathf.Pow(1f - p, intervalSeconds);
        }

        public static bool IsGpuThing(Thing thing)
        {
            return thing?.TryGetComp<CompGPUCluster>() != null;
        }

        public static bool CanBreakdownNow(CompBreakdownable comp)
        {
            if (comp == null || comp.BrokenDown)
            {
                return false;
            }

            CompPowerTrader powerComp = comp.parent.GetComp<CompPowerTrader>();
            return powerComp == null || powerComp.PowerOn;
        }
    }

    [HarmonyPatch(typeof(CompBreakdownable), nameof(CompBreakdownable.CheckForBreakdown))]
    public static class Patch_CompBreakdownable_CheckForBreakdown
    {
        public static bool Prefix(CompBreakdownable __instance)
        {
            if (!GpuBreakdownRiskUtility.IsGpuThing(__instance?.parent))
            {
                return true;
            }

            if (!GpuBreakdownRiskUtility.CanBreakdownNow(__instance))
            {
                return false;
            }

            float perSecondChance = GpuBreakdownRiskUtility.ComputeBreakChancePerSecond(__instance.parent);
            float perCheckChance = GpuBreakdownRiskUtility.ComputeBreakChancePerCheck(perSecondChance);
            if (Rand.Chance(perCheckChance))
            {
                __instance.DoBreakdown();
            }

            return false;
        }
    }
}