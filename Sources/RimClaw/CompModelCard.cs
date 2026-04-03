using System.Collections.Generic;
using Verse;

namespace RimClaw
{
    public class CompProperties_ModelCard : CompProperties
    {
        public List<string> namePrefixes = new List<string> { "kwen" };
        public List<string> nameSuffixes = new List<string> { "3.5_27b" };
        public int requiredVramMin = 20;
        public int requiredVramMax = 120;
        public float tokenPerSecondPerInstanceMin = 80f;
        public float tokenPerSecondPerInstanceMax = 260f;
        public float workSpeedBonusMin = 0.01f;
        public float workSpeedBonusMax = 0.06f;

        public CompProperties_ModelCard()
        {
            compClass = typeof(CompModelCard);
        }
    }

    public class CompModelCard : ThingComp
    {
        private bool initialized;
        private string modelName;
        private int requiredVram;
        private float tokenPerSecondPerInstance;
        private float workSpeedBonus;

        public CompProperties_ModelCard Props => (CompProperties_ModelCard)props;

        public string ModelName => modelName;
        public int RequiredVram => requiredVram;
        public float TokenPerSecondPerInstance => tokenPerSecondPerInstance;
        public float WorkSpeedBonus => workSpeedBonus;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", defaultValue: false);
            Scribe_Values.Look(ref modelName, "modelName");
            Scribe_Values.Look(ref requiredVram, "requiredVram", 40);
            Scribe_Values.Look(ref tokenPerSecondPerInstance, "tokenPerSecondPerInstance", 200f);
            Scribe_Values.Look(ref workSpeedBonus, "workSpeedBonus", 0.02f);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            string prefix = Props.namePrefixes.NullOrEmpty() ? "kwen" : Props.namePrefixes.RandomElement();
            string suffix = Props.nameSuffixes.NullOrEmpty() ? "3.5_27b" : Props.nameSuffixes.RandomElement();
            modelName = prefix + suffix;
            requiredVram = Rand.RangeInclusive(Props.requiredVramMin, Props.requiredVramMax);
            tokenPerSecondPerInstance = Rand.Range(Props.tokenPerSecondPerInstanceMin, Props.tokenPerSecondPerInstanceMax);
            workSpeedBonus = Rand.Range(Props.workSpeedBonusMin, Props.workSpeedBonusMax);
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();
            return $"Model: {modelName}\nRequired VRAM: {requiredVram} GB\nToken/s per instance: {tokenPerSecondPerInstance:0}\nGlobal work speed: +{workSpeedBonus * 100f:0.0}%";
        }
    }
}
