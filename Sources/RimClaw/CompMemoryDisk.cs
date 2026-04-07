using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_MemoryDisk : CompProperties
    {
        public int searchRadius = 12;
        public float modelSignOffsetX = 0f;
        public float modelSignOffsetY = 0.42f;
        public float modelSignOffsetZ = 0f;
        public float modelSignScale = 0.8f;
        public float modelSignAlpha = 0.8f;
        public float modelSignBobAmplitudeTiles = 0.1f;
        public float modelSignBobPeriodSeconds = 4f;

        public CompProperties_MemoryDisk()
        {
            compClass = typeof(CompMemoryDisk);
        }
    }

    public class CompMemoryDisk : ThingComp
    {
        private Mote modelSignMote;

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
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.MapHeld != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Insert nearby model",
                    defaultDesc = "Consume one nearby model package and store it into this disk.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", reportFailure: false),
                    action = InsertNearbyModel
                };
            }

            if (hasModel)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject model",
                    defaultDesc = "Clear current stored model from disk.",
                    action = delegate
                    {
                        hasModel = false;
                        modelName = null;
                        modelColor = Color.white;
                        requiredVram = 0;
                        tokenPerSecondPerInstance = 0f;
                        workSpeedBonus = 0f;
                        DestroyModelSignMote();
                    }
                };
            }
        }

        public override string CompInspectStringExtra()
        {
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

            if (parent?.MapHeld == null || !parent.Spawned)
            {
                DestroyModelSignMote();
                return;
            }

            if (!hasModel)
            {
                DestroyModelSignMote();
                return;
            }

            EnsureModelSignMote();
            if (modelSignMote == null || modelSignMote.Destroyed)
            {
                return;
            }
        }

        private void EnsureModelSignMote()
        {
            if (modelSignMote != null && !modelSignMote.Destroyed)
            {
                return;
            }

            ThingDef moteDef = RimClawDefOf.RimClaw_Mote_ModelDiskSign;
            if (moteDef == null)
            {
                return;
            }

            modelSignMote = MoteMaker.MakeAttachedOverlay(parent, moteDef, new Vector3(Props.modelSignOffsetX, Props.modelSignOffsetY, Props.modelSignOffsetZ), Mathf.Max(0.05f, Props.modelSignScale), -1f);
            Mote_ModelDiskSign sign = modelSignMote as Mote_ModelDiskSign;
            if (sign != null)
            {
                sign.bobAmplitudeTiles = Props.modelSignBobAmplitudeTiles;
                sign.bobPeriodSeconds = Props.modelSignBobPeriodSeconds;
            }

            modelSignMote.instanceColor = new Color(modelColor.r, modelColor.g, modelColor.b, Mathf.Clamp01(Props.modelSignAlpha));
        }

        private void DestroyModelSignMote()
        {
            if (modelSignMote != null && !modelSignMote.Destroyed)
            {
                modelSignMote.Destroy();
            }

            modelSignMote = null;
        }

        private void InsertNearbyModel()
        {
            if (parent.MapHeld == null)
            {
                return;
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.MapHeld, Props.searchRadius, useCenter: true))
            {
                if (thing.def != RimClawDefOf.RimClaw_ModelCard)
                {
                    continue;
                }

                CompModelCard comp = thing.TryGetComp<CompModelCard>();
                if (comp == null)
                {
                    continue;
                }

                string label = thing.LabelCap;
                opts.Add(new FloatMenuOption(label, delegate
                {
                    comp.EnsureInitialized();
                    hasModel = true;
                    modelName = comp.ModelName;
                    modelColor = comp.ModelColor;
                    requiredVram = comp.RequiredVram;
                    tokenPerSecondPerInstance = comp.TokenPerSecondPerInstance;
                    workSpeedBonus = comp.WorkSpeedBonus;
                    thing.Destroy(DestroyMode.Vanish);
                }));
            }

            if (opts.Count == 0)
            {
                Messages.Message("No model package found in range.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

    }
}
