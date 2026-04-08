using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public enum PromptInjectorOutcome
    {
        Join = 0,
        Berserk = 1,
        SelfDelete = 2
    }

    public class CompProperties_PromptInjectorReaction : CompProperties
    {
        public CompProperties_PromptInjectorReaction()
        {
            compClass = typeof(CompPromptInjectorReaction);
        }
    }

    public class CompPromptInjectorReaction : ThingComp
    {
        private bool pending;
        private int executeTick;
        private PromptInjectorOutcome outcome;

        private Pawn Pawn => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref pending, "pending", false);
            Scribe_Values.Look(ref executeTick, "executeTick", 0);
            Scribe_Values.Look(ref outcome, "outcome", PromptInjectorOutcome.Join);
        }

        public void Schedule(PromptInjectorOutcome nextOutcome, int delayTicks)
        {
            pending = true;
            executeTick = Find.TickManager.TicksGame + delayTicks;
            outcome = nextOutcome;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!pending || !parent.IsHashIntervalTick(10))
            {
                return;
            }

            if (Find.TickManager.TicksGame < executeTick)
            {
                return;
            }

            pending = false;
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned)
            {
                return;
            }

            ApplyOutcome(Pawn, outcome);
        }

        public static void ApplyOutcome(Pawn target, PromptInjectorOutcome outcome)
        {
            switch (outcome)
            {
                case PromptInjectorOutcome.Join:
                    JoinAsClawfish(target);
                    break;
                case PromptInjectorOutcome.Berserk:
                    target.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk, forceWake: true);
                    break;
                default:
                    Job selfDelete = JobMaker.MakeJob(RimClawDefOf.RimClaw_SelfDelete);
                    target.jobs?.TryTakeOrderedJob(selfDelete, JobTag.Misc);
                    break;
            }
        }

        private static void JoinAsClawfish(Pawn target)
        {
            Map map = target.Map;
            IntVec3 cell = target.Position;
            if (map == null)
            {
                return;
            }

            Color fishColor = target.TryGetComp<CompClawfishColor>()?.Color ?? ClawfishUtility.GetOrAssignColor(target);
            string pawnName = target.Name?.ToStringFull ?? ClawfishUtility.GenerateName();

            Pawn colonist = PawnGenerator.GeneratePawn(RimClawConfig.ConvertedPawnKind, Faction.OfPlayer);

            if (colonist.story != null)
            {
                BackstoryDef child = DefDatabase<BackstoryDef>.GetNamedSilentFail("RimClaw_Childhood_Crustacean");
                BackstoryDef adult = DefDatabase<BackstoryDef>.GetNamedSilentFail("RimClaw_Adulthood_LLM_Agent");
                if (child != null)
                {
                    colonist.story.Childhood = child;
                }

                if (adult != null)
                {
                    colonist.story.Adulthood = adult;
                }
            }

            ClawfishUtility.EnsureAiTraitForLlmAgent(colonist);

            if (colonist.skills != null)
            {
                foreach (SkillRecord skill in colonist.skills.skills)
                {
                    skill.Level = RimClawConfig.Values.clawfishSkillLevel;
                    skill.xpSinceLastLevel = 0f;
                    skill.passion = Passion.None;
                }
            }

            ClawfishUtility.EnsureSkillFloor(colonist, 8);

            CompClawfishColor colorComp = colonist.TryGetComp<CompClawfishColor>();
            if (colorComp != null)
            {
                colorComp.Initialize(fishColor);
            }

            colonist.Name = new NameSingle(pawnName);
            GenSpawn.Spawn(colonist, cell, map, WipeMode.Vanish);
            colonist.needs?.AddOrRemoveNeedsAsAppropriate();
            colonist.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(colonist);
            target.Destroy(DestroyMode.Vanish);

            RimClawConfigExtension cfg = RimClawConfig.Values;

            Find.LetterStack.ReceiveLetter(
                cfg.promptInjectorSuccessLetterLabel,
                string.Format(cfg.promptInjectorSuccessLetterText, colonist.LabelShortCap),
                LetterDefOf.PositiveEvent,
                colonist);
        }
    }
}
