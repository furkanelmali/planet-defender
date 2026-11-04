using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetDefender
{
    // Everything one run owns. Discarded at settlement; nothing here is permanent (GDD §3.1, §8).
    public sealed class RunState
    {
        public string runId;
        public PlanetConfig planet;
        public int hp, maxHp, level = 1, xp, score, sessionGold, goldEarned, goldSpent, kills, sectorsPassed;
        public float elapsed;
        public bool reviveUsed, settled;
        public readonly Dictionary<string, int> upgrades = new Dictionary<string, int>();

        public int killStreak;
        public float Combo { get; private set; } = 1f;

        public static RunState Fresh(PlanetConfig planet) => new RunState
        {
            runId = Guid.NewGuid().ToString("N"),
            planet = planet,
            hp = planet.hp,
            maxHp = planet.hp,
        };

        public static RunState FromSnapshot(RunSnapshot s, GameConfig config)
        {
            var run = new RunState
            {
                runId = s.runId, planet = config.Planet(s.planet), hp = Mathf.Max(1, s.hp), level = Mathf.Max(1, s.level), xp = s.xp,
                score = s.score, sessionGold = s.sessionGold, goldEarned = s.goldEarned, goldSpent = s.goldSpent, kills = s.kills,
                elapsed = s.elapsedSeconds, reviveUsed = s.reviveUsed,
            };
            run.maxHp = Mathf.Max(run.planet.hp, run.hp);
            // Older saves have no sector count: already-passed milestones must not pay out again.
            run.sectorsPassed = Mathf.Max(s.sectorsPassed, config.SectorsAt(s.elapsedSeconds));
            if (s.upgrades != null) foreach (var u in s.upgrades) run.upgrades[u.id] = u.level;
            return run;
        }

        public RunSnapshot ToSnapshot()
        {
            var s = new RunSnapshot
            {
                runId = runId, planet = planet.id, hp = hp, level = level, xp = xp, score = score, sessionGold = sessionGold,
                goldEarned = goldEarned, goldSpent = goldSpent, kills = kills, reviveUsed = reviveUsed, elapsedSeconds = elapsed, runSettled = settled, sectorsPassed = sectorsPassed,
            };
            foreach (var pair in upgrades) s.upgrades.Add(new UpgradeLevel { id = pair.Key, level = pair.Value });
            return s;
        }

        public int Level(string upgrade) => upgrades.TryGetValue(upgrade, out int l) ? l : 0;
        public int XpNeeded => GameConfig.XpForLevel(level);

        // GDD §6: every 5 consecutive kills +0.25x, capped at 3x; a planet hit drops it back to 1x.
        public void RegisterKill(ComboConfig combo)
        {
            kills++;
            killStreak++;
            Combo = Mathf.Min(combo.max, 1f + combo.step * (killStreak / Mathf.Max(1, combo.killsPerStep)));
        }
        public void BreakCombo() { killStreak = 0; Combo = 1f; }
    }

    // Derived numbers for the current planet passive + bought upgrades, with the GDD's hard caps.
    public sealed class PlayerStats
    {
        public float fireInterval, damage, doubleSpread, doubleOffset, explosionRadius, explosionDamage;
        public float freezeDuration, freezeSlow, droneInterval, laserCooldown, laserDps;
        public float xpMultiplier = 1, goldMultiplier = 1, meteorSpeedMultiplier = 1;
        public int shots = 1, pierce, drones;
        public bool hasShield, hasSlowPulse;
        public float shieldPeriod, slowPulsePeriod, slowPulseDuration, slowPulseAmount;

        public static PlayerStats Compute(GameConfig config, RunState run)
        {
            var p = run.planet;
            var s = new PlayerStats();
            float interval = config.player.fireInterval;
            if (p.passive == "fireRate") interval *= 1f - p.value;
            interval *= Mathf.Pow(1f - config.Upgrade("rapid_fire").values[0], run.Level("rapid_fire"));
            s.fireInterval = Mathf.Max(config.caps.minFireInterval, interval);

            s.damage = config.player.bulletDamage * (p.passive == "damage" ? 1f + p.value : 1f)
                       * (1f + config.Upgrade("damage_boost").values[0] * run.Level("damage_boost"));

            int doubleLevel = run.Level("double_shot");
            if (doubleLevel > 0)
            {
                var up = config.Upgrade("double_shot");
                s.shots = 2; s.doubleSpread = up.Value(doubleLevel); s.doubleOffset = up.Value2(doubleLevel);
            }
            s.pierce = Mathf.RoundToInt(config.Upgrade("piercing").Value(run.Level("piercing")));

            int explosion = run.Level("explosion");
            s.explosionRadius = config.Upgrade("explosion").Value(explosion);
            s.explosionDamage = explosion > 0 ? config.Upgrade("explosion").Value2(explosion) * s.damage : 0;

            int freeze = run.Level("freeze_pulse");
            s.freezeDuration = config.Upgrade("freeze_pulse").Value(freeze);
            s.freezeSlow = config.Upgrade("freeze_pulse").Value2(freeze);

            int drone = run.Level("orbital_drone");
            s.drones = Mathf.RoundToInt(config.Upgrade("orbital_drone").Value(drone));
            s.droneInterval = config.Upgrade("orbital_drone").Value2(drone);

            int laser = run.Level("laser_beam");
            s.laserCooldown = config.Upgrade("laser_beam").Value(laser);
            s.laserDps = config.Upgrade("laser_beam").Value2(laser) * s.damage;

            if (p.passive == "xpBonus") s.xpMultiplier = 1f + p.value;
            if (p.passive == "goldBonus") s.goldMultiplier = 1f + p.value;
            if (p.passive == "meteorSlow") s.meteorSpeedMultiplier = 1f - p.value;
            if (p.passive == "shield") { s.hasShield = true; s.shieldPeriod = p.period; }
            if (p.passive == "slowPulse") { s.hasSlowPulse = true; s.slowPulsePeriod = p.period; s.slowPulseDuration = p.duration; s.slowPulseAmount = p.value; }
            return s;
        }
    }
}
