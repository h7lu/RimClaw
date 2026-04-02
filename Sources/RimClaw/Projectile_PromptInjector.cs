using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class Projectile_PromptInjector : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield)
            {
                return;
            }

            if (hitThing is not Pawn target || !ClawfishUtility.IsClawfish(target) || target.Dead)
            {
                return;
            }

            if (Rand.Chance(RimClawSettings.PromptInjectorStunChance))
            {
                target.stances?.stunner?.StunFor(RimClawSettings.PromptInjectorStunTicks, launcher, addBattleLog: true, showMote: true, disableRotation: false);
            }

            float roll = Rand.Value;
            if (roll < 0.60f)
            {
                ConvertToColonist(target);
                return;
            }

            if (roll < 0.90f)
            {
                target.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk, forceWake: true);
                return;
            }

            Job selfDelete = JobMaker.MakeJob(RimClawDefOf.RimClaw_SelfDelete);
            target.jobs?.TryTakeOrderedJob(selfDelete, JobTag.Misc);
        }

        private static void ConvertToColonist(Pawn target)
        {
            Map map = target.Map;
            IntVec3 cell = target.Position;

            if (map == null)
            {
                return;
            }

            Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(colonist, cell, map, WipeMode.Vanish);
            colonist.Name = new NameSingle(ClawfishUtility.GenerateName());
            target.Destroy(DestroyMode.Vanish);
        }
    }
}
