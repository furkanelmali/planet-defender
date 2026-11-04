using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetDefender
{
    public sealed class ActiveMission
    {
        public MissionConfig config;
        public MissionState state;
        public int tier, target, reward;

        public float Fraction => target <= 0 ? 1f : Mathf.Clamp01((float)state.progress / target);
        public string Title => Loc.F("mission_" + config.id, Format(target));
        public string Progress => $"{Format(Mathf.Min(state.progress, target))}/{Format(target)}";

        string Format(int value) => config.stat == "survive" ? UIKit.Clock(value) : UIKit.Num(value);
    }

    // Three missions are always active. Finishing one pays session gold on the spot (so it can buy an upgrade right away and
    // reaches the bank through normal settlement) and brings in the next type from the rotation, one tier harder than last time.
    // Progress is saved with the rest of the document at the usual breaks, never per kill.
    public static class MissionService
    {
        static GameConfig config;
        static int lastKills, lastGold, lastUpgrades;
        public static int CompletedThisRun { get; private set; }
        public static int RewardThisRun { get; private set; }
        public static event Action<ActiveMission> Completed;

        public static void Init(GameConfig gameConfig)
        {
            config = gameConfig;
            var data = SaveService.Data;
            data.missions.RemoveAll(m => Config(m.id) == null);
            while (data.missions.Count < Mathf.Min(3, config.missions.Length)) data.missions.Add(NextMission());
        }

        static MissionConfig Config(string id)
        {
            foreach (var m in config.missions) if (m.id == id) return m;
            return null;
        }

        static int Tier(string id)
        {
            foreach (var t in SaveService.Data.missionTiers) if (t.id == id) return t.level;
            return 0;
        }

        static void RaiseTier(string id)
        {
            foreach (var t in SaveService.Data.missionTiers) if (t.id == id) { t.level++; return; }
            SaveService.Data.missionTiers.Add(new UpgradeLevel { id = id, level = 1 });
        }

        static MissionState NextMission()
        {
            var data = SaveService.Data;
            for (int tries = 0; tries < config.missions.Length; tries++)
            {
                var candidate = config.missions[data.missionCursor++ % config.missions.Length];
                if (!data.missions.Exists(m => m.id == candidate.id)) return new MissionState { id = candidate.id };
            }
            return new MissionState { id = config.missions[0].id };
        }

        public static List<ActiveMission> Active()
        {
            var list = new List<ActiveMission>();
            foreach (var state in SaveService.Data.missions)
            {
                var cfg = Config(state.id);
                if (cfg == null) continue;
                int tier = Tier(state.id);
                list.Add(new ActiveMission { config = cfg, state = state, tier = tier, target = cfg.Target(tier), reward = cfg.Reward(tier) });
            }
            return list;
        }

        // The goal most likely to pull a player into one more run.
        public static ActiveMission Closest()
        {
            ActiveMission best = null;
            foreach (var m in Active()) if (best == null || m.Fraction > best.Fraction) best = m;
            return best;
        }

        static int UpgradeCount(RunState run)
        {
            int total = 0;
            foreach (var pair in run.upgrades) total += pair.Value;
            return total;
        }

        public static void BeginRun(RunState run)
        {
            lastKills = run.kills; lastGold = run.goldEarned; lastUpgrades = UpgradeCount(run);
            CompletedThisRun = 0; RewardThisRun = 0;
        }

        public static void Tick(RunState run)
        {
            if (run == null || config == null) return;
            int kills = run.kills - lastKills, gold = run.goldEarned - lastGold, upgrades = UpgradeCount(run) - lastUpgrades;
            lastKills = run.kills; lastGold = run.goldEarned; lastUpgrades = UpgradeCount(run);
            var data = SaveService.Data;
            for (int i = 0; i < data.missions.Count; i++)
            {
                var state = data.missions[i];
                var cfg = Config(state.id);
                if (cfg == null) continue;
                switch (cfg.stat)
                {
                    case "kills": state.progress += kills; break;
                    case "gold": state.progress += gold; break;
                    case "upgrades": state.progress += upgrades; break;
                    // Single-run goals keep the player's best attempt: a near miss stays visible between runs.
                    case "survive": state.progress = Mathf.Max(state.progress, Mathf.FloorToInt(run.elapsed)); break;
                    case "level": state.progress = Mathf.Max(state.progress, run.level); break;
                    case "streak": state.progress = Mathf.Max(state.progress, run.killStreak); break;
                    case "score": state.progress = Mathf.Max(state.progress, run.score); break;
                }
                int tier = Tier(state.id), target = cfg.Target(tier);
                if (state.progress < target) continue;

                int reward = cfg.Reward(tier);
                run.sessionGold += reward;
                run.goldEarned += reward;
                lastGold += reward;   // a reward is not "collected gold" for the gold mission
                CompletedThisRun++;
                RewardThisRun += reward;
                data.missionsDone++;
                var done = new ActiveMission { config = cfg, state = state, tier = tier, target = target, reward = reward };
                RaiseTier(state.id);
                data.missions[i] = NextMission();
                PlaygamaBridgeAdapter.Analytics("mission_complete", new Dictionary<string, object>
                    { { "mission", cfg.id }, { "tier", tier }, { "reward", reward }, { "total_done", data.missionsDone } });
                Completed?.Invoke(done);
            }
        }
    }

    // Seven-day login ladder: a reason to come back tomorrow. Missing a day restarts it at day one.
    public static class DailyService
    {
        static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static int DayOffsetForTests;
        public static int Today => (int)(DateTime.UtcNow - Epoch).TotalDays + DayOffsetForTests;
        public static bool Pending => SaveService.Data.dailyDay != Today;
        public static int NextStreak => SaveService.Data.dailyDay == Today - 1 ? SaveService.Data.dailyStreak + 1 : 1;
        public static int Reward(GameConfig config, int streak) => config.daily.rewards[(Mathf.Max(1, streak) - 1) % config.daily.rewards.Length];

        public static int Claim(GameConfig config, bool doubled)
        {
            if (!Pending) return 0;
            var data = SaveService.Data;
            int streak = NextStreak, amount = Reward(config, streak) * (doubled ? 2 : 1);
            data.dailyStreak = streak;
            data.dailyDay = Today;
            EconomyService.AddBank(amount);
            PlaygamaBridgeAdapter.Analytics("daily_claim", new Dictionary<string, object> { { "streak", streak }, { "amount", amount }, { "doubled", doubled } });
            return amount;
        }
    }
}
