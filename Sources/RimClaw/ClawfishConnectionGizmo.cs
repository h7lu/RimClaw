using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace RimClaw
{
    public static class ClawfishConnectionGizmo
    {
        public static void GetGizmosForClawfish(Pawn pawn, List<Gizmo> gizmos)
        {
            if (!ClawfishUtility.IsClawfishColonist(pawn))
                return;

            var tokenConn = pawn.TryGetComp<CompClawfishTokenConnection>();
            if (tokenConn == null)
                return;

            if (tokenConn.IsConnected)
            {
                // Show disconnect gizmo
                gizmos.Add(GetDisconnectGizmo(pawn, tokenConn));
            }
            else
            {
                // Show connect gizmo
                gizmos.Add(GetConnectGizmo(pawn, tokenConn));
            }
        }

        private static Gizmo GetConnectGizmo(Pawn pawn, CompClawfishTokenConnection tokenConn)
        {
            return new Command_Action
            {
                defaultLabel = "Connect to LLM Service",
                defaultDesc = "Click and select a Host Computer or LLM Subscription to connect this clawfish's token supply.",
                icon = ContentFinder<Texture2D>.Get("connect_llm", true),
                action = delegate
                {
                    Find.Targeter.BeginTargeting(
                        new TargetingParameters
                        {
                            canTargetBuildings = true,
                            canTargetPawns = false,
                            validator = (TargetInfo target) =>
                            {
                                if (!target.HasThing)
                                    return false;

                                CompHostComputerService host = target.Thing.TryGetComp<CompHostComputerService>();
                                if (host != null)
                                {
                                    return host.IsValidSupplier;
                                }

                                CompTokenSupplier supplier = target.Thing.TryGetComp<CompTokenSupplier>();
                                return supplier != null && supplier.IsValidSupplier;
                            }
                        },
                        action: (LocalTargetInfo target) =>
                        {
                            if (target.Thing != null)
                            {
                                tokenConn.Connect(target.Thing);

                                CompHostComputerService host = target.Thing.TryGetComp<CompHostComputerService>();
                                if (host != null)
                                {
                                    host.AddConnectedClaw(pawn);
                                    Messages.Message($"{pawn.Name} connected to {target.Thing.Label}", MessageTypeDefOf.PositiveEvent);
                                    return;
                                }

                                CompTokenSupplier supplier = target.Thing.TryGetComp<CompTokenSupplier>();
                                if (supplier != null)
                                {
                                    supplier.AddConnectedClaw(pawn);
                                    Messages.Message($"{pawn.Name} connected to {target.Thing.Label}", MessageTypeDefOf.PositiveEvent);
                                }
                            }
                        }
                    );
                }
            };
        }

        private static Gizmo GetDisconnectGizmo(Pawn pawn, CompClawfishTokenConnection tokenConn)
        {
            return new Command_Action
            {
                defaultLabel = "Disconnect from LLM Service",
                defaultDesc = $"Disconnect this clawfish from {tokenConn.ConnectedSupplier.Label}.",
                icon = ContentFinder<Texture2D>.Get("disconnect_llm", true),
                action = delegate
                {
                    CompHostComputerService host = tokenConn.ConnectedSupplier.TryGetComp<CompHostComputerService>();
                    if (host != null)
                    {
                        host.RemoveConnectedClaw(pawn);
                    }

                    CompTokenSupplier supplier = tokenConn.ConnectedSupplier.TryGetComp<CompTokenSupplier>();
                    if (supplier != null)
                    {
                        supplier.RemoveConnectedClaw(pawn);
                    }

                    tokenConn.Disconnect();
                    Messages.Message($"{pawn.Name} disconnected from service", MessageTypeDefOf.NeutralEvent);
                }
            };
        }
    }
}
