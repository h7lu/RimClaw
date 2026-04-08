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
        public float formingGraphicAlpha = 0.8f;
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
        private Material pendingInsertLineMat;

        private bool hasModel;
        private string modelName;
        private Color modelColor = Color.white;
        private int requiredVram;
        private float tokenPerSecondPerInstance;
        private float workSpeedBonus;
        private int modelSkillLevelAdjustment;
        private int hostThingID = -1;

        public CompProperties_MemoryDisk Props => (CompProperties_MemoryDisk)props;
        public bool HasModel => hasModel;
        public string ModelName => modelName;
        public Color ModelColor => modelColor;
        public int RequiredVram => requiredVram;
        public float TokenPerSecondPerInstance => tokenPerSecondPerInstance;
        public float WorkSpeedBonus => workSpeedBonus;
        public int ModelSkillLevelAdjustment => modelSkillLevelAdjustment;
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
            Scribe_Values.Look(ref modelSkillLevelAdjustment, "modelSkillLevelAdjustment", 0);
            Scribe_Values.Look(ref hostThingID, "hostThingID", -1);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.MapHeld != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "RimClaw_MemoryDisk_InsertModel_Label".Translate(),
                    defaultDesc = "RimClaw_MemoryDisk_InsertModel_Desc".Translate(),
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
                    defaultLabel = "RimClaw_MemoryDisk_EjectModel_Label".Translate(),
                    defaultDesc = "RimClaw_MemoryDisk_EjectModel_Desc".Translate(),
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
            if (!hasModel)
            {
                return hostThingID < 0
                    ? "RimClaw_MemoryDisk_Inspect_None".Translate()
                    : "RimClaw_MemoryDisk_Inspect_NoneWithHost".Translate(hostThingID);
            }

            return hostThingID < 0
                ? "RimClaw_MemoryDisk_Inspect_WithModel".Translate(modelName, requiredVram, tokenPerSecondPerInstance.ToString("0"), (workSpeedBonus * 100f).ToString("0.0"), (modelSkillLevelAdjustment >= 0 ? "+" : string.Empty) + modelSkillLevelAdjustment)
                : "RimClaw_MemoryDisk_Inspect_WithModelHost".Translate(modelName, requiredVram, tokenPerSecondPerInstance.ToString("0"), (workSpeedBonus * 100f).ToString("0.0"), (modelSkillLevelAdjustment >= 0 ? "+" : string.Empty) + modelSkillLevelAdjustment, hostThingID);
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
                UpdateGlowerColor();
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

            if (hasModel)
            {
                if (parent?.MapHeld == null)
                {
                    return false;
                }

                Thing previousPackage = ThingMaker.MakeThing(RimClawDefOf.RimClaw_ModelCard);
                CompModelCard previousCard = previousPackage.TryGetComp<CompModelCard>();
                previousCard?.OverrideModelData(modelName, modelColor, requiredVram, tokenPerSecondPerInstance, workSpeedBonus, modelSkillLevelAdjustment);

                bool placedPrevious = GenPlace.TryPlaceThing(previousPackage, parent.InteractionCell, parent.MapHeld, ThingPlaceMode.Near, out Thing _);
                if (!placedPrevious)
                {
                    previousPackage.Destroy(DestroyMode.Vanish);
                        Messages.Message("RimClaw_MemoryDisk_ReinstallEjectFailed".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
            }

            hasModel = true;
            modelName = comp.ModelName;
            modelColor = comp.ModelColor;
            requiredVram = comp.RequiredVram;
            tokenPerSecondPerInstance = comp.TokenPerSecondPerInstance;
            workSpeedBonus = comp.WorkSpeedBonus;
            modelSkillLevelAdjustment = comp.ModelSkillLevelAdjustment;

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

            CompProperties_MemoryDisk compProperties = Props;
            GraphicData formingGraphicData = compProperties?.formingGraphicData;
            if (formingGraphicData != null)
            {
                Vector3 loc = parent.DrawPos + compProperties.GetRotationOffset(parent.Rotation);
                int ticksGame = Find.TickManager?.TicksGame ?? 0;
                float bob = Mathf.PingPong(ticksGame * compProperties.formingMechBobSpeed, compProperties.formingMechYBobDistance);
                loc.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + compProperties.formingGraphicYOffset + bob;

                Graphic graphic = formingGraphicData.Graphic;
                Color tintedColor = modelColor;
                tintedColor.a *= compProperties.formingGraphicAlpha;
                Material tintedMat = MaterialPool.MatFrom(formingGraphicData.texPath, graphic.Shader, tintedColor);
                Mesh mesh = graphic.MeshAt(parent.Rotation);
                Vector3 scale = new Vector3(2f, 1f, 2f);
                Matrix4x4 matrix = Matrix4x4.TRS(loc, Quaternion.identity, scale);
                Graphics.DrawMesh(mesh, matrix, tintedMat, 0);
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();

            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns = parent.MapHeld.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                Job curJob = p?.CurJob;
                if (curJob == null || curJob.def != RimClawDefOf.RimClaw_InsertModelIntoDisk)
                {
                    continue;
                }

                if (curJob.targetA.Thing != parent)
                {
                    continue;
                }

                Thing package = curJob.targetB.Thing;
                if (package?.Spawned == true && package.Map == parent.MapHeld)
                {
                    if (pendingInsertLineMat == null)
                    {
                        pendingInsertLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.98f, 0.86f, 0.22f, 1f));
                    }

                    GenDraw.DrawLineBetween(parent.DrawPos, package.DrawPos, pendingInsertLineMat);
                }
            }
        }

        public void ClearStoredModel()
        {
            hasModel = false;
            modelName = null;
            modelColor = Color.white;
            requiredVram = 0;
            tokenPerSecondPerInstance = 0f;
            workSpeedBonus = 0f;
            modelSkillLevelAdjustment = 0;
            UpdateGlowerColor();
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
                    Messages.Message("RimClaw_MemoryDisk_SelectModelPackage".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Pawn worker = FindBestInsertionPawn(selectedThing);
                if (worker == null)
                {
                    Messages.Message("RimClaw_MemoryDisk_NoInsertionPawn".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Job job = JobMaker.MakeJob(RimClawDefOf.RimClaw_InsertModelIntoDisk, parent, selectedThing);
                job.count = 1;
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
            card?.OverrideModelData(modelName, modelColor, requiredVram, tokenPerSecondPerInstance, workSpeedBonus, modelSkillLevelAdjustment);

            bool placed = GenPlace.TryPlaceThing(package, parent.InteractionCell, parent.MapHeld, ThingPlaceMode.Near, out Thing _);
            if (!placed)
            {
                package.Destroy(DestroyMode.Vanish);
                Messages.Message("RimClaw_MemoryDisk_EjectPlaceFailed".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            ClearStoredModel();
        }

        private void UpdateGlowerColor()
        {
            if (parent?.Spawned != true || parent.MapHeld == null)
            {
                return;
            }

            CompGlower glower = parent.TryGetComp<CompGlower>();
            if (glower == null)
            {
                return;
            }

            if (!hasModel)
            {
                glower.GlowRadius = 0f;
                glower.ForceRegister(parent.MapHeld);
                return;
            }

            glower.GlowRadius = 3f;
            Color color = RimClawGlowUtility.SoftenToGlow(modelColor);
            ColorInt colorInt = new ColorInt(
                Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                0);

            if (glower.GlowColor != colorInt)
            {
                glower.GlowColor = colorInt;
            }

            glower.ForceRegister(parent.MapHeld);
        }
    }
}
