using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimClaw
{
    public class CompProperties_MemoryDisk : CompProperties
    {
        private const string DebugPrefix = "[RimClaw][MemoryDisk][Props]";

        public int searchRadius = 12;
        public GraphicData formingGraphicData;
        public float formingMechBobSpeed = 0.0007f;
        public float formingMechYBobDistance = 0.06f;
        public float formingGraphicYOffset = 0.018292684f;
        public Vector3 northOffset = new Vector3(0f, 0f, 0.18f);
        public Vector3 eastOffset = new Vector3(0f, 0f, 0.18f);
        public Vector3 southOffset = new Vector3(0f, 0f, 0.18f);
        public Vector3 westOffset = new Vector3(0f, 0f, 0.18f);

        public CompProperties_MemoryDisk()
        {
            compClass = typeof(CompMemoryDisk);
        }

        public Vector3 GetRotationOffset(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 1:
                    return eastOffset;
                case 2:
                    return southOffset;
                case 3:
                    return westOffset;
                default:
                    return northOffset;
            }
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string configError in base.ConfigErrors(parentDef))
            {
                yield return configError;
            }

            if (formingGraphicData == null)
            {
                yield return $"{DebugPrefix} missing formingGraphicData on {parentDef?.defName}";
            }
        }
    }

    public class CompMemoryDisk : ThingComp
    {
        private const string DebugPrefix = "[RimClaw][MemoryDisk][Comp]";

        private bool hasModel;
        private string modelName;
        private Color modelColor = Color.white;
        private int requiredVram;
        private float tokenPerSecondPerInstance;
        private float workSpeedBonus;
        private int hostThingID = -1;

        public CompProperties_MemoryDisk Props => (CompProperties_MemoryDisk)props;
        public bool HasModel => hasModel;
        public string ModelName => modelName;
        public Color ModelColor => modelColor;
        public int RequiredVram => requiredVram;
        public float TokenPerSecondPerInstance => tokenPerSecondPerInstance;
        public float WorkSpeedBonus => workSpeedBonus;
        public int HostThingID => hostThingID;

        public void AssignHost(Thing host)
        {
            hostThingID = host?.thingIDNumber ?? -1;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasModel, "hasModel", defaultValue: false);
            Scribe_Values.Look(ref modelName, "modelName");
            Scribe_Values.Look(ref modelColor, "modelColor", Color.white);
            Scribe_Values.Look(ref requiredVram, "requiredVram", 0);
            Scribe_Values.Look(ref tokenPerSecondPerInstance, "tokenPerSecondPerInstance", 0f);
            Scribe_Values.Look(ref workSpeedBonus, "workSpeedBonus", 0f);
            Scribe_Values.Look(ref hostThingID, "hostThingID", -1);
            Log.Message($"{DebugPrefix} PostExposeData thing={(parent != null ? parent.ThingID.ToString() : "null")} loaded={Scribe.mode} hasModel={hasModel} modelName={modelName ?? "(null)"} host={hostThingID} requiredVram={requiredVram} tps={tokenPerSecondPerInstance:0.###} workBonus={workSpeedBonus:0.###}");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Log.Message($"{DebugPrefix} CompGetGizmosExtra thing={(parent != null ? parent.ThingID.ToString() : "null")} spawned={(parent?.Spawned ?? false)} mapHeld={(parent?.MapHeld != null)} hasModel={hasModel} modelName={modelName ?? "(null)"}");
            if (parent.MapHeld != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Insert model",
                    defaultDesc = "Select a model package on the ground, then assign a pawn to carry and insert it.",
                    icon = ContentFinder<Texture2D>.Get("Insert_Model", reportFailure: false),
                    action = delegate
                    {
                        StartSelectModelTarget();
                    }
                };
            }

            if (hasModel)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject model",
                    defaultDesc = "Eject current model as a model package item.",
                    icon = ContentFinder<Texture2D>.Get("Eject_Model", reportFailure: false),
                    action = delegate
                    {
                        TryEjectModelAsPackage();
                    }
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            Log.Message($"{DebugPrefix} CompInspectStringExtra thing={(parent != null ? parent.ThingID.ToString() : "null")} hasModel={hasModel} modelName={modelName ?? "(null)"} host={hostThingID}");
            if (!hasModel)
            {
                return hostThingID < 0 ? "Model: (none)" : $"Model: (none)\nHost ID: {hostThingID}";
            }

            return hostThingID < 0
                ? $"Model: {modelName}\nRequired VRAM: {requiredVram} GB\nToken/s per instance: {tokenPerSecondPerInstance:0}\nWork speed bonus: +{workSpeedBonus * 100f:0.0}%"
                : $"Model: {modelName}\nRequired VRAM: {requiredVram} GB\nToken/s per instance: {tokenPerSecondPerInstance:0}\nWork speed bonus: +{workSpeedBonus * 100f:0.0}%\nHost ID: {hostThingID}";
        }

        public override void CompTick()
        {
            base.CompTick();
            if (hasModel && parent != null && parent.Spawned && parent.IsHashIntervalTick(30))
            {
                RimClawGlowUtility.SpawnPulseGlow(parent, RimClawGlowUtility.SoftenToGlow(modelColor), 4f);
            }

            if (parent != null && parent.IsHashIntervalTick(60))
            {
                Log.Message($"{DebugPrefix} CompTick thing={parent.ThingID} spawned={parent.Spawned} mapHeld={(parent.MapHeld != null)} hasModel={hasModel} modelName={modelName ?? "(null)"} host={hostThingID}");
            }
        }

        public bool TryStoreModelFromCardThing(Thing thing, bool consumeThing)
        {
            if (thing == null || thing.def != RimClawDefOf.RimClaw_ModelCard)
            {
                return false;
            }

            CompModelCard comp = thing.TryGetComp<CompModelCard>();
            if (comp == null)
            {
                return false;
            }

            comp.EnsureInitialized();
            hasModel = true;
            modelName = comp.ModelName;
            modelColor = comp.ModelColor;
            requiredVram = comp.RequiredVram;
            tokenPerSecondPerInstance = comp.TokenPerSecondPerInstance;
            workSpeedBonus = comp.WorkSpeedBonus;

            if (consumeThing)
            {
                thing.Destroy(DestroyMode.Vanish);
            }

            return true;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent?.Spawned != true || !hasModel)
            {
                return;
            }

            RimClawGlowUtility.DrawGlow(parent.DrawPos, RimClawGlowUtility.SoftenToGlow(modelColor), 4f);
        }

        public void ClearStoredModel()
        {
            hasModel = false;
            modelName = null;
            modelColor = Color.white;
            requiredVram = 0;
            tokenPerSecondPerInstance = 0f;
            workSpeedBonus = 0f;
        }

        private void StartSelectModelTarget()
        {
            if (parent.MapHeld == null)
            {
                return;
            }

            TargetingParameters parms = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetItems = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                mapObjectTargetsMustBeAutoAttackable = false
            };
            parms.validator = target => target.HasThing && target.Thing.def == RimClawDefOf.RimClaw_ModelCard;

            Find.Targeter.BeginTargeting(parms, delegate(LocalTargetInfo target)
            {
                Thing selectedThing = target.Thing;
                if (selectedThing == null || selectedThing.def != RimClawDefOf.RimClaw_ModelCard)
                {
                    Messages.Message("Select a model package.", parent, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Pawn worker = FindBestInsertionPawn(selectedThing);
                if (worker == null)
                {
                    Messages.Message("No available pawn can insert this model package.", parent, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Job job = JobMaker.MakeJob(RimClawDefOf.RimClaw_InsertModelIntoDisk, parent, selectedThing);
                worker.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }

        private Pawn FindBestInsertionPawn(Thing modelPackage)
        {
            if (parent?.MapHeld == null || modelPackage == null)
            {
                return null;
            }

            Pawn best = null;
            float bestDistance = float.MaxValue;
            List<Pawn> pawns = parent.MapHeld.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || pawn.WorkTagIsDisabled(WorkTags.Hauling))
                {
                    continue;
                }

                if (!pawn.CanReserveAndReach(modelPackage, PathEndMode.ClosestTouch, Danger.Some))
                {
                    continue;
                }

                if (!pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some))
                {
                    continue;
                }

                float dist = (pawn.Position - modelPackage.Position).LengthHorizontalSquared;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    best = pawn;
                }
            }

            return best;
        }

        private void TryEjectModelAsPackage()
        {
            if (!hasModel || parent?.MapHeld == null)
            {
                return;
            }

            Thing package = ThingMaker.MakeThing(RimClawDefOf.RimClaw_ModelCard);
            CompModelCard card = package.TryGetComp<CompModelCard>();
            card?.OverrideModelData(modelName, modelColor, requiredVram, tokenPerSecondPerInstance, workSpeedBonus);

            bool placed = GenPlace.TryPlaceThing(package, parent.InteractionCell, parent.MapHeld, ThingPlaceMode.Near, out Thing _);
            if (!placed)
            {
                package.Destroy(DestroyMode.Vanish);
                Messages.Message("Could not place ejected model package.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            ClearStoredModel();
        }

    }
}
