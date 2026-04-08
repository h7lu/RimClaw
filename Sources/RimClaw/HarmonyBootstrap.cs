using HarmonyLib;
using Verse;

namespace RimClaw
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            Log.Message("[RimClaw][Harmony] Bootstrap starting");
            var harmony = new Harmony("openclaw.rimclaw");
            harmony.PatchAll();
            Log.Message("[RimClaw][Harmony] Bootstrap finished");
        }
    }
}
