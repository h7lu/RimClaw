using System;
using HarmonyLib;
using RimWorld;

namespace RimClaw
{
    [HarmonyPatch(typeof(GoodwillSituationWorker_SameIdeo), nameof(GoodwillSituationWorker_SameIdeo.GetNaturalGoodwillOffset))]
    public static class Patch_GoodwillSituationWorker_SameIdeo_GetNaturalGoodwillOffset
    {
        public static Exception Finalizer(Exception __exception, ref int __result)
        {
            if (__exception is NullReferenceException)
            {
                __result = 0;
                return null;
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(GoodwillSituationWorker_MemeCompatibility), nameof(GoodwillSituationWorker_MemeCompatibility.GetNaturalGoodwillOffset))]
    public static class Patch_GoodwillSituationWorker_MemeCompatibility_GetNaturalGoodwillOffset
    {
        public static Exception Finalizer(Exception __exception, ref int __result)
        {
            if (__exception is NullReferenceException)
            {
                __result = 0;
                return null;
            }

            return __exception;
        }
    }
}
