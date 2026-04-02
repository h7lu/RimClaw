namespace RimClaw
{
    public static class RimClawSettings
    {
        public const float DefaultFishingSpawnChance = 0.10f;
        public const int DefaultSpawnRadiusFromFisher = 3;

        public const float DefaultPromptInjectorStunChance = 0.50f;
        public const int DefaultPromptInjectorStunTicks = 300;

        public const float DefaultPromptInjectorJoinChance = 0.60f;
        public const float DefaultPromptInjectorBerserkChance = 0.30f;
        public const float DefaultPromptInjectorSelfDeleteChance = 0.10f;

        public const int DefaultSelfDeleteTicks = 300;
        public const int DefaultClawfishSkillLevel = 8;

        public const int DefaultNameMinPid = 10000;
        public const int DefaultNameMaxPid = 999999;

        public static readonly string[] DefaultNameFragments =
        {
            "Auto", "Vector", "Kernel", "Delta", "Omega", "Cloud", "Prompt", "Runtime", "Signal", "Code"
        };
    }
}
