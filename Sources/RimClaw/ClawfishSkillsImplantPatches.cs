using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimClaw
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_SkillsImplantUninstall
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }

            if (!ClawfishUtility.IsClawfish(__instance))
            {
                yield break;
            }

            int skillsMdCount = SkillsImplantUtility.GetInstalledSkillsMdCount(__instance);
            if (skillsMdCount > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = RimClawDefOf.RimClaw_SkillsMd.GetCompProperties<CompProperties_SkillsMd>()?.uninstallLabel ?? "Uninstall Skills.md",
                    defaultDesc = string.Format(RimClawDefOf.RimClaw_SkillsMd.GetCompProperties<CompProperties_SkillsMd>()?.uninstallDesc ?? "Remove one of {0} installed Skills.md implant(s).", skillsMdCount),
                    icon = ContentFinder<Texture2D>.Get("uninstall_skill_md", reportFailure: false),
                    action = delegate
                    {
                        ShowUninstallMenu(__instance);
                    }
                };
            }
        }

        private static void ShowUninstallMenu(Pawn clawfish)
        {
            List<Hediff_SkillsImplant> implants = SkillsImplantUtility.GetInstalledSkillsMdList(clawfish);
            if (implants.Count == 0)
            {
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < implants.Count; i++)
            {
                Hediff_SkillsImplant implant = implants[i];
                string label = implant.Codename ?? RimClawConfig.Values.genericUnnamedText;
                int index = i;
                options.Add(new FloatMenuOption(label, delegate
                {
                    StartUninstallJob(clawfish, index);
                }));
            }

            FloatMenu menu = new FloatMenu(options);
            Find.WindowStack.Add(menu);
        }

        private static void StartUninstallJob(Pawn clawfish, int index)
        {
            if (clawfish?.Map == null)
            {
                return;
            }

            Job job = JobMaker.MakeJob(RimClawDefOf.RimClaw_UninstallSkillsMd, clawfish);
            job.count = index;
            clawfish.jobs.TryTakeOrderedJob(job);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString_ContextCorruption
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!ClawfishUtility.IsClawfish(__instance))
            {
                return;
            }

            float corruptionRate = SkillsImplantUtility.GetContextCorruptionRateFromSkillsMd(__instance);
            if (corruptionRate > 0f)
            {
                __result += "\n" + string.Format(DefDatabase<ThingDef>.GetNamedSilentFail("RimClaw_SkillsMd")?.GetCompProperties<CompProperties_SkillsMd>()?.contextCorruptionInspect ?? "Context corruption (from Skills.md): {0}%", (corruptionRate * 100f).ToString("0.0"));
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpecialDisplayStats))]
    public static class Patch_Pawn_SpecialDisplayStats_ContextCorruption
    {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, Pawn __instance)
        {
            foreach (StatDrawEntry entry in __result)
            {
                yield return entry;
            }

            if (!ClawfishUtility.IsClawfish(__instance))
            {
                yield break;
            }

            float corruptionRate = SkillsImplantUtility.GetContextCorruptionRateFromSkillsMd(__instance);
            if (corruptionRate > 0f)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    DefDatabase<ThingDef>.GetNamedSilentFail("RimClaw_SkillsMd")?.GetCompProperties<CompProperties_SkillsMd>()?.corruptionRateLabel ?? "Skills.md corruption rate",
                    $"{corruptionRate * 100f:0.0}%",
                    DefDatabase<ThingDef>.GetNamedSilentFail("RimClaw_SkillsMd")?.GetCompProperties<CompProperties_SkillsMd>()?.corruptionRateDesc ?? "Context corruption caused by installed Skills.md implants.",
                    2000);
            }
        }
    }
}
