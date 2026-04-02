using HarmonyLib;
using Verse;

namespace RimClaw
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            var harmony = new Harmony("openclaw.rimclaw");
            harmony.PatchAll();
        }
    }
}
