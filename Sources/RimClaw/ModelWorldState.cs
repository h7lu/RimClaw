using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class TagModelStats : IExposable
    {
        public string tag;
        public float tokenPerSecondMultiplier;
        public float vramMultiplier;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref tokenPerSecondMultiplier, "tokenPerSecondMultiplier", 1f);
            Scribe_Values.Look(ref vramMultiplier, "vramMultiplier", 1f);
        }
    }

    public class FactionModelProfile : IExposable
    {
        public string factionId;
        public string formalName;
        public Color modelColor = Color.white;
        public float feeRateMultiplier = 1f;
        public float workSpeedMultiplier = 1f;
        public List<TagModelStats> tags = new List<TagModelStats>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionId, "factionId");
            Scribe_Values.Look(ref formalName, "formalName");
            Scribe_Values.Look(ref modelColor, "modelColor", Color.white);
            Scribe_Values.Look(ref feeRateMultiplier, "feeRateMultiplier", 1f);
            Scribe_Values.Look(ref workSpeedMultiplier, "workSpeedMultiplier", 1f);
            Scribe_Collections.Look(ref tags, "tags", LookMode.Deep);
            if (tags == null)
            {
                tags = new List<TagModelStats>();
            }
        }

        public TagModelStats GetTag(string name)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i].tag, name, StringComparison.OrdinalIgnoreCase))
                {
                    return tags[i];
                }
            }

            return null;
        }
    }

    public class RimClawModelWorldState : GameComponent
    {
        private static readonly string[] DefaultFormalNamePool =
        {
            "Clode", "Astra", "Nexa", "Orin", "Vanta", "Helix", "Quanta", "Myria", "Luma", "Cinder",
            "Arcus", "Radian", "Solace", "Kestrel", "Nova", "Caelum", "Iris", "Verdan", "Talon", "Echo"
        };

        private static readonly string[] DefaultMidWordPool =
        {
            "Flash", "Rapid", "Fast", "Instruct", "Lite", "Linear", "PagedAttn", "Mini", "Thinking", "Deepthink", "Pro", "Max", "Omni"
        };

        private static readonly string[] DefaultTpsOrderPool =
        {
            "Flash", "Rapid", "Fast", "Instruct", "Lite", "Linear", "PagedAttn", "Mini", "None", "Thinking", "Deepthink", "Pro", "Max", "Omni"
        };

        private static readonly string[] DefaultVramOrderPool =
        {
            "Omni", "Max", "Pro", "Deepthink", "Thinking", "None", "Instruct", "Rapid", "PagedAttn", "Fast", "Flash", "Mini", "Lite", "Linear"
        };

        private List<FactionModelProfile> profiles = new List<FactionModelProfile>();

        public RimClawModelWorldState(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsureProfilesForExistingFactions();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref profiles, "profiles", LookMode.Deep);
            if (profiles == null)
            {
                profiles = new List<FactionModelProfile>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureProfilesForExistingFactions();
            }
        }

        public static RimClawModelWorldState Instance
        {
            get
            {
                if (Current.Game == null)
                {
                    return null;
                }

                return Current.Game.GetComponent<RimClawModelWorldState>();
            }
        }

        public FactionModelProfile GetProfileForThing(Thing thing)
        {
            Faction faction = thing?.Faction ?? thing?.Map?.ParentFaction;
            if (faction == null)
            {
                return GetRandomFactionProfile();
            }

            return GetOrCreateProfile(faction);
        }

        public FactionModelProfile GetRandomFactionProfile()
        {
            EnsureProfilesForExistingFactions();
            if (profiles.Count == 0)
            {
                var fallback = new FactionModelProfile
                {
                    factionId = "Fallback",
                    formalName = "Clode",
                    modelColor = Color.white,
                    feeRateMultiplier = 1f,
                    workSpeedMultiplier = 1f,
                    tags = GenerateTagStats()
                };
                profiles.Add(fallback);
                return fallback;
            }

            return profiles.RandomElement();
        }

        public FactionModelProfile GetOrCreateProfile(Faction faction)
        {
            EnsureProfilesForExistingFactions();
            string id = faction.GetUniqueLoadID();
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].factionId == id)
                {
                    return profiles[i];
                }
            }

            FactionModelProfile created = CreateProfileForFaction(faction, profiles.Select(p => p.formalName).ToHashSet());
            profiles.Add(created);
            return created;
        }

        public string GenerateMidName(FactionModelProfile profile, out float tokenMultiplier, out float vramMultiplier)
        {
            bool includeNumeric = Rand.Chance(0.65f);
            bool includeWord = Rand.Chance(0.65f);
            if (!includeNumeric && !includeWord)
            {
                includeWord = true;
            }

            List<string> parts = new List<string>();
            List<float> tpsValues = new List<float>();
            List<float> vramValues = new List<float>();

            if (includeNumeric)
            {
                float numeric = (float)Math.Round(Rand.Range(1.0f, 9.9f), 1);
                parts.Add(numeric.ToString("0.0"));
                float numericMul = Mathf.Clamp(0.5f + ((numeric - 1f) / 8.9f) * 1.5f, 0.5f, 2f);
                tpsValues.Add(numericMul);
                vramValues.Add(numericMul);
            }

            if (includeWord)
            {
                string[] candidates = GetConfiguredPool(RimClawConfig.Values.modelMidWordPool, DefaultMidWordPool);
                string chosen = candidates[Rand.Range(0, candidates.Length)];
                parts.Add(chosen);

                TagModelStats stats = profile.GetTag(chosen) ?? profile.GetTag("None");
                tpsValues.Add(stats?.tokenPerSecondMultiplier ?? 1f);
                vramValues.Add(stats?.vramMultiplier ?? 1f);
            }

            if (parts.Count == 2 && Rand.Chance(0.5f))
            {
                string tmp = parts[0];
                parts[0] = parts[1];
                parts[1] = tmp;
            }

            tokenMultiplier = Mathf.Clamp(tpsValues.Average(), 0.5f, 2f);
            vramMultiplier = Mathf.Clamp(vramValues.Average(), 0.5f, 2f);
            return string.Concat(parts);
        }

        private void EnsureProfilesForExistingFactions()
        {
            if (Find.FactionManager == null)
            {
                return;
            }

            HashSet<string> usedFormalNames = profiles.Select(p => p.formalName).ToHashSet();
            List<Faction> allFactions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < allFactions.Count; i++)
            {
                Faction faction = allFactions[i];
                if (faction == null)
                {
                    continue;
                }

                string id = faction.GetUniqueLoadID();
                bool exists = profiles.Any(p => p.factionId == id);
                if (!exists)
                {
                    FactionModelProfile profile = CreateProfileForFaction(faction, usedFormalNames);
                    usedFormalNames.Add(profile.formalName);
                    profiles.Add(profile);
                }
            }
        }

        private static FactionModelProfile CreateProfileForFaction(Faction faction, HashSet<string> usedFormalNames)
        {
            string formal = PickUniqueFormalName(usedFormalNames);
            Color modelColor = GenerateFactionModelColor(faction.Color);
            return new FactionModelProfile
            {
                factionId = faction.GetUniqueLoadID(),
                formalName = formal,
                modelColor = modelColor,
                feeRateMultiplier = Rand.Range(0.5f, 2f),
                workSpeedMultiplier = Rand.Range(0.5f, 2f),
                tags = GenerateTagStats()
            };
        }

        private static string PickUniqueFormalName(HashSet<string> used)
        {
            string[] configuredPool = GetConfiguredPool(RimClawConfig.Values.modelFormalNamePool, DefaultFormalNamePool);
            List<string> pool = new List<string>(configuredPool);
            pool.Shuffle();
            for (int i = 0; i < pool.Count; i++)
            {
                if (!used.Contains(pool[i]))
                {
                    return pool[i];
                }
            }

            int suffix = 2;
            while (used.Contains("Clode" + suffix))
            {
                suffix++;
            }

            return "Clode" + suffix;
        }

        private static Color GenerateFactionModelColor(Color factionColor)
        {
            Vector3 center = new Vector3(factionColor.r * 255f, factionColor.g * 255f, factionColor.b * 255f);
            for (int i = 0; i < 64; i++)
            {
                Vector3 rgb = center + RandomInsideSphere(30f);
                rgb.x = Mathf.Clamp(rgb.x, 0f, 255f);
                rgb.y = Mathf.Clamp(rgb.y, 0f, 255f);
                rgb.z = Mathf.Clamp(rgb.z, 0f, 255f);

                Color candidate = new Color(rgb.x / 255f, rgb.y / 255f, rgb.z / 255f, 1f);
                Color.RGBToHSV(candidate, out float h, out float s, out float v);
                if (s > 0.7f && v > 0.5f)
                {
                    return candidate;
                }
            }

            Color.RGBToHSV(factionColor, out float hue, out _, out _);
            return Color.HSVToRGB(hue, 0.8f, 0.8f);
        }

        private static List<TagModelStats> GenerateTagStats()
        {
            List<TagModelStats> results = new List<TagModelStats>();

            string[] tpsOrder = (string[])GetConfiguredPool(RimClawConfig.Values.modelTpsOrderPool, DefaultTpsOrderPool).Clone();
            string[] vramOrder = (string[])GetConfiguredPool(RimClawConfig.Values.modelVramOrderPool, DefaultVramOrderPool).Clone();
            RandomNeighborSwap(tpsOrder);
            RandomNeighborSwap(vramOrder);

            Dictionary<string, float> tps = BuildOrderedMultipliers(tpsOrder);
            Dictionary<string, float> vram = BuildOrderedMultipliers(vramOrder);

            HashSet<string> keys = new HashSet<string>(tps.Keys);
            keys.UnionWith(vram.Keys);
            foreach (string key in keys)
            {
                results.Add(new TagModelStats
                {
                    tag = key,
                    tokenPerSecondMultiplier = tps.TryGetValue(key, out float t) ? t : 1f,
                    vramMultiplier = vram.TryGetValue(key, out float v) ? v : 1f
                });
            }

            return results;
        }

        private static Dictionary<string, float> BuildOrderedMultipliers(string[] ordered)
        {
            Dictionary<string, float> map = new Dictionary<string, float>();
            int count = ordered.Length;
            for (int i = 0; i < count; i++)
            {
                float t = (count <= 1) ? 0f : (float)i / (count - 1);
                float value = Mathf.Lerp(2.0f, 0.5f, t) + Rand.Range(-0.04f, 0.04f);
                map[ordered[i]] = Mathf.Clamp(value, 0.5f, 2.0f);
            }

            return map;
        }

        private static void RandomNeighborSwap(string[] values)
        {
            if (values.Length < 2 || !Rand.Chance(0.35f))
            {
                return;
            }

            int idx = Rand.Range(0, values.Length - 1);
            string temp = values[idx];
            values[idx] = values[idx + 1];
            values[idx + 1] = temp;
        }

        private static Vector3 RandomInsideSphere(float radius)
        {
            float u = Rand.Value;
            float v = Rand.Value;
            float w = Rand.Value;
            float theta = 2f * Mathf.PI * u;
            float phi = Mathf.Acos(2f * v - 1f);
            float r = radius * Mathf.Pow(w, 1f / 3f);
            float sinPhi = Mathf.Sin(phi);

            return new Vector3(
                r * sinPhi * Mathf.Cos(theta),
                r * sinPhi * Mathf.Sin(theta),
                r * Mathf.Cos(phi));
        }

        private static string[] GetConfiguredPool(List<string> configured, string[] fallback)
        {
            if (configured == null || configured.Count == 0)
            {
                return fallback;
            }

            List<string> cleaned = new List<string>();
            for (int i = 0; i < configured.Count; i++)
            {
                string value = configured[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    cleaned.Add(value.Trim());
                }
            }

            if (cleaned.Count == 0)
            {
                return fallback;
            }

            return cleaned.ToArray();
        }
    }
}