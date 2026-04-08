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
                    defaultLabel = "RimClaw_Skills_Uninstall_Label".Translate(),
                    defaultDesc = "RimClaw_Skills_Uninstall_Desc".Translate(skillsMdCount),
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
                string label = implant.Codename ?? "RimClaw_Generic_Unnamed".Translate().ToString();
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
                __result += "\n" + "RimClaw_Skills_ContextCorruptionInspect".Translate((corruptionRate * 100f).ToString("0.0"));
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
                    "RimClaw_Skills_CorruptionRate_Label".Translate(),
                    $"{corruptionRate * 100f:0.0}%",
                    "RimClaw_Skills_CorruptionRate_Desc".Translate(),
                    2000);
            }
        }
    }
}
