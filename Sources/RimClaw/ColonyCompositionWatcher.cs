using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimClaw
{
    public class ColonyCompositionWatcher : GameComponent
    {
        private int lastColonistSignature;
        private bool allClawColonyTriggered;

        public ColonyCompositionWatcher(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastColonistSignature, "lastColonistSignature", 0);
            Scribe_Values.Look(ref allClawColonyTriggered, "allClawColonyTriggered", false);
        }

        public override void GameComponentTick()
        {
            if (allClawColonyTriggered)
            {
                return;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            if (ticksGame % 120 != 0)
            {
                return;
            }

            IReadOnlyList<Pawn> colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
            int signature = ComputeSignature(colonists);
            if (signature == lastColonistSignature)
            {
                return;
            }

            lastColonistSignature = signature;
            TryHandlePersonnelChange(colonists);
        }

        private static int ComputeSignature(IReadOnlyList<Pawn> colonists)
        {
            unchecked
            {
                int hash = 17;
                List<int> ids = colonists.Select(p => p?.thingIDNumber ?? 0).OrderBy(id => id).ToList();
                for (int i = 0; i < ids.Count; i++)
                {
                    hash = hash * 31 + ids[i];
                }

                return hash;
            }
        }

        private void TryHandlePersonnelChange(IReadOnlyList<Pawn> colonists)
        {
            bool hasAnyColonist = colonists != null && colonists.Count > 0;
            if (!hasAnyColonist)
            {
                return;
            }

            bool allClawColonists = true;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null)
                {
                    continue;
                }

                if (!ClawfishUtility.IsClawfishColonist(pawn))
                {
                    allClawColonists = false;
                    break;
                }
            }

            if (!allClawColonists)
            {
                return;
            }

            Faction mechHive = Find.FactionManager?.FirstFactionOfDef(FactionDefOf.Mechanoid);
            Faction player = Faction.OfPlayer;
            if (mechHive == null || player == null)
            {
                return;
            }

            bool relationChanged = false;

            if (mechHive.def.permanentEnemy)
            {
                mechHive.def.permanentEnemy = false;
                relationChanged = true;
            }

            if (mechHive.Hidden)
            {
                mechHive.hidden = false;
                relationChanged = true;
            }

            FactionRelation rel = mechHive.RelationWith(player);
            if (rel != null)
            {
                if (rel.baseGoodwill != 20)
                {
                    rel.baseGoodwill = 20;
                    relationChanged = true;
                }

                try
                {
                    int currentGoodwill = mechHive.GoodwillWith(player);
                    if (currentGoodwill < 100)
                    {
                        int delta = 100 - currentGoodwill;
                        mechHive.TryAffectGoodwillWith(player, delta, canSendMessage: false, canSendHostilityLetter: false);
                        relationChanged = true;
                    }
                }
                catch
                {
                    // Keep game stable even if other goodwill workers misbehave.
                }
            }

            RimClawConfigExtension cfg = RimClawConfig.Values;
            string body = relationChanged
                ? cfg.allClawColonyLetterTextChanged
                : cfg.allClawColonyLetterTextUnchanged;

            Find.LetterStack.ReceiveLetter(cfg.allClawColonyLetterLabel, body, LetterDefOf.PositiveEvent);
            allClawColonyTriggered = true;
        }
    }
}
