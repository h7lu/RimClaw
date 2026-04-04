using RimWorld;
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
            if (target.Faction != Faction.OfPlayer)
            {
                target.SetFaction(Faction.OfPlayer);
            }

            target.mindState?.mentalStateHandler?.Reset();
            target.jobs?.StopAll();
            ClawfishUtility.ApplyBaseline(target);

            Messages.Message($"{target.LabelShortCap} joined your faction.", target, MessageTypeDefOf.PositiveEvent, historical: false);
        }
    }
}
