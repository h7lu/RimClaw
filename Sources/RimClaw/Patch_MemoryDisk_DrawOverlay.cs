using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public static class Patch_MemoryDisk_DrawOverlay
    {
        public static void Prefix(Graphic __instance, Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            // Render path intentionally moved to CompMemoryDisk.PostDraw.
        }
    }
}