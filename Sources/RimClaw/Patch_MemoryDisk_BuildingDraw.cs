using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public static class Patch_MemoryDisk_BuildingDraw
    {
        public static void Postfix(ThingWithComps __instance, Vector3 drawLoc, bool flip)
        {
            // Render path intentionally moved to CompMemoryDisk.PostDraw.
        }
    }
}