using RimWorld;
using Verse;

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

            bool stunned = false;
            if (Rand.Chance(cfg.promptInjectorStunChance))
            {
                target.stances?.stunner?.StunFor(cfg.promptInjectorStunTicks, launcher, addBattleLog: true, showMote: true, disableRotation: false);
                stunned = true;
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
            PromptInjectorOutcome outcome;
            if (roll < joinChance)
            {
                outcome = PromptInjectorOutcome.Join;
            }
            else if (roll < joinChance + berserkChance)
            {
                outcome = PromptInjectorOutcome.Berserk;
            }
            else
            {
                outcome = PromptInjectorOutcome.SelfDelete;
            }

            if (stunned)
            {
                CompPromptInjectorReaction comp = target.TryGetComp<CompPromptInjectorReaction>();
                if (comp != null)
                {
                    comp.Schedule(outcome, cfg.promptInjectorStunTicks);
                    return;
                }
            }

            CompPromptInjectorReaction.ApplyOutcome(target, outcome);
        }
    }
}
