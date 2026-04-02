using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class Projectile_PromptInjector : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var cfg = RimClawConfig.Values;
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield)
            {
                return;
            }

            if (hitThing is not Pawn target || !ClawfishUtility.IsClawfish(target) || target.Dead)
            {
                return;
            }

            if (Rand.Chance(cfg.promptInjectorStunChance))
            {
                target.stances?.stunner?.StunFor(cfg.promptInjectorStunTicks, launcher, addBattleLog: true, showMote: true, disableRotation: false);
            }

            float joinChance = System.Math.Max(0f, cfg.promptInjectorJoinChance);
            float berserkChance = System.Math.Max(0f, cfg.promptInjectorBerserkChance);
            float selfDeleteChance = System.Math.Max(0f, cfg.promptInjectorSelfDeleteChance);
            float sum = joinChance + berserkChance + selfDeleteChance;
            if (sum <= 0.0001f)
            {
                joinChance = 0.60f;
                berserkChance = 0.30f;
                selfDeleteChance = 0.10f;
                sum = 1f;
            }

            joinChance /= sum;
            berserkChance /= sum;

            float roll = Rand.Value;
            if (roll < joinChance)
            {
                ConvertToColonist(target);
                return;
            }

            if (roll < joinChance + berserkChance)
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

            Pawn colonist = PawnGenerator.GeneratePawn(RimClawConfig.ConvertedPawnKind, Faction.OfPlayer);
            GenSpawn.Spawn(colonist, cell, map, WipeMode.Vanish);
            colonist.Name = new NameSingle(ClawfishUtility.GenerateName());
            target.Destroy(DestroyMode.Vanish);
        }
    }
}
