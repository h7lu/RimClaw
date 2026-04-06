using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class CompProperties_MemoryDisk : CompProperties
    {
        public int searchRadius = 12;

        public CompProperties_MemoryDisk()
        {
            compClass = typeof(CompMemoryDisk);
        }
    }

    public class CompMemoryDisk : ThingComp
    {
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
