using System;
using UnityEngine;

namespace PlanetDefender
{
    // Every tunable number of the GDD lives in Resources/Config/game_config.json; code reads only this model.
    [Serializable] public sealed class GameConfig
    {
        public PlayerConfig player;
        public SpawnConfig spawn;
        public GoldConfig gold;
        public ComboConfig combo;
        public MeteorConfig[] meteors;
        public PlanetConfig[] planets;
        public UpgradeConfig[] upgrades;
        public float upgradePriceGrowth = 1.65f;
        public float freezePulsePeriod = 20f;
        public float laserDuration = 0.7f;
        public float laserWidth = 0.32f;
        public float droneRange = 6.5f;
        public CapsConfig caps;
        public SectorConfig sectors = new SectorConfig();
        public AdConfig ads = new AdConfig();
        public FirstRunConfig firstRun = new FirstRunConfig();
        public DailyConfig daily = new DailyConfig();
        public MissionConfig[] missions = new MissionConfig[0];

        static GameConfig cached;
        public static GameConfig Load()
        {
            if (cached != null) return cached;
            var text = Resources.Load<TextAsset>("Config/game_config");
            if (text == null) throw new InvalidOperationException("Missing Resources/Config/game_config.json");
            cached = JsonUtility.FromJson<GameConfig>(text.text);
            return cached;
        }

        public MeteorConfig Meteor(string id) { foreach (var m in meteors) if (m.id == id) return m; return meteors[0]; }
        public PlanetConfig Planet(string id) { foreach (var p in planets) if (p.id == id) return p; return planets[0]; }
        public UpgradeConfig Upgrade(string id) { foreach (var u in upgrades) if (u.id == id) return u; return null; }
        public int PlanetIndex(string id) { for (int i = 0; i < planets.Length; i++) if (planets[i].id == id) return i; return 0; }

        // Sector milestones: the first one comes early (a payoff inside the first 30 s), then one per minute.
        public float SectorTime(int index) => index <= 0 ? sectors.firstAt : sectors.every * index;
        public int SectorBonus(int index) => sectors.baseBonus + sectors.bonusStep * Mathf.Max(0, index);
        public int SectorsAt(float elapsed) { int n = 0; while (elapsed >= SectorTime(n)) n++; return n; }
        public string ThreatAt(int index)
        {
            float time = SectorTime(index);
            foreach (var m in meteors) if (m.unlockAt > 0 && Mathf.Approximately(m.unlockAt, time)) return m.id;
            return null;
        }

        // GDD §7: 100 + 40(L-1) + 5(L-1)^2.
        public static int XpForLevel(int level) { int n = level - 1; return 100 + 40 * n + 5 * n * n; }

        // GDD §8: ceil(firstPrice × 1.65^currentTier).
        public int UpgradePrice(UpgradeConfig upgrade, int currentTier) =>
            Mathf.CeilToInt(upgrade.basePrice * Mathf.Pow(upgradePriceGrowth, currentTier) - 0.0001f);
    }

    [Serializable] public sealed class PlayerConfig
    {
        public float fireInterval = 0.3f, bulletSpeed = 15f, bulletDamage = 1f, bulletRadius = 0.13f, planetRadius = 1f;
        public float reviveInvulnerability = 2f, reviveClearRadius = 3.2f;
    }

    [Serializable] public sealed class SpawnConfig
    {
        public float tutorialSeconds = 20, startInterval = 1.8f, minInterval = 0.32f, rampEvery = 30, rampFactor = 1.07f;
        public float baseSpeed = 1.25f, travelNormalize = 0.5f, referenceDistance = 5f, speedRampPerStep = 0.025f, maxSpeedMultiplier = 1.5f, edgeMargin = 0.9f;
        public int capMobile = 25, capDesktop = 40;
        public float burstFrom = 150, burstChance = 0.18f;
    }

    [Serializable] public sealed class GoldVariant { public string id; public int value; public float weight; public float radius; }
    [Serializable] public sealed class GoldConfig
    {
        public float minInterval = 6, maxInterval = 9, pityMax = 11, speed = 1.15f;
        public GoldVariant[] variants;
    }

    [Serializable] public sealed class ComboConfig { public int killsPerStep = 5; public float step = 0.25f, max = 3f; }

    [Serializable] public sealed class MeteorConfig
    {
        public string id;
        public int hp, score, xp, splitInto;
        public float speedMult, radius, unlockAt, weight;
        public string splitType;
    }

    [Serializable] public sealed class PlanetConfig
    {
        public string id, passive, texture, atmosphere;
        public int price, hp, ring;
        public float value, period, duration, visualScale, spin;
        public Color AtmosphereColor => ColorUtility.TryParseHtmlString(atmosphere, out var c) ? c : Color.cyan;
    }

    [Serializable] public sealed class UpgradeConfig
    {
        public string id;
        public int basePrice, maxLevel;
        public float[] values, values2;
        public float Value(int level) => level <= 0 || values == null || values.Length == 0 ? 0 : values[Mathf.Min(level, values.Length) - 1];
        public float Value2(int level) => level <= 0 || values2 == null || values2.Length == 0 ? 0 : values2[Mathf.Min(level, values2.Length) - 1];
    }

    [Serializable] public sealed class SectorConfig { public float firstAt = 30, every = 60; public int baseBonus = 15, bonusStep = 15, bigFrom = 2; }
    [Serializable] public sealed class AdConfig { public float firstInterstitialAfter = 45, interstitialCooldown = 60; public int doubleGoldMin = 20; }
    [Serializable] public sealed class FirstRunConfig { public float assistSeconds = 45; }
    [Serializable] public sealed class DailyConfig { public int[] rewards = { 50, 75, 100, 150, 200, 300, 500 }; }

    [Serializable] public sealed class MissionConfig
    {
        public string id, stat;
        public bool perRun;   // true: one run must reach the target; false: progress adds up across runs
        public float start, step, growth = 1, reward, rewardGrowth = 1;

        public int Target(int tier)
        {
            int value = Mathf.RoundToInt((start + step * tier) * Mathf.Pow(growth, tier));
            return value >= 20 ? Mathf.RoundToInt(value / 5f) * 5 : value;
        }
        public int Reward(int tier) => Mathf.RoundToInt(reward * Mathf.Pow(rewardGrowth, tier) / 5f) * 5;
    }

    [Serializable] public sealed class CapsConfig { public float minFireInterval = 0.12f, maxSlow = 0.6f; }
}
