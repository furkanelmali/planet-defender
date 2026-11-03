using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Leaderboards;
#endif

namespace PlanetDefender
{
    public sealed class LeaderboardEntry { public int rank; public string name; public int score; public bool isPlayer; }

    // GDD §11: Bridge SaaS leaderboard planet_defender_best_score. Never shows invented rivals: on not_available the
    // button is hidden and only the personal best is shown.
    public static class LeaderboardService
    {
        public const string Id = "planet_defender_best_score";
        public enum Mode { NotAvailable, InGame, Native, NativePopup }
        static bool submitting;

        public static Mode Type
        {
            get
            {
#if UNITY_WEBGL
                switch (Bridge.leaderboards.type)
                {
                    case LeaderboardType.InGame: return Mode.InGame;
                    case LeaderboardType.Native: return Mode.Native;
                    case LeaderboardType.NativePopup: return Mode.NativePopup;
                }
#endif
                return Mode.NotAvailable;
            }
        }

        // The ranking button is always on screen. Where the platform has no global board (not_available / native), the
        // screen shows the personal best and says the global ranking lives on Playgama — never an invented list.
        public static bool HasGlobalList => Type == Mode.InGame;

        public static void ShowNativePopup()
        {
#if UNITY_WEBGL
            Bridge.leaderboards.ShowNativePopup(Id);
#endif
        }

        public static void GetEntries(Action<bool, List<LeaderboardEntry>> done)
        {
#if UNITY_WEBGL
            Bridge.leaderboards.GetEntries(Id, (success, rows) =>
            {
                var list = new List<LeaderboardEntry>();
                if (success && rows != null)
                {
                    foreach (var row in rows)
                    {
                        var entry = new LeaderboardEntry
                        {
                            rank = ParseInt(row, "rank", list.Count + 1),
                            name = Pick(row, "name", "playerName", "id"),
                            score = ParseInt(row, "score", 0),
                            isPlayer = Pick(row, "id") == Playgama.Bridge.player.id && !string.IsNullOrEmpty(Playgama.Bridge.player.id)
                        };
                        if (string.IsNullOrEmpty(entry.name)) entry.name = "—";
                        list.Add(entry);
                    }
                }
                done?.Invoke(success, list);
            });
#else
            done?.Invoke(false, null);
#endif
        }

        // Only a new personal best is sent. A failure keeps bestScorePending so the next boot or menu visit retries.
        public static void SubmitPendingBest()
        {
            var data = SaveService.Data;
            if (!data.bestScorePending || submitting || Type == Mode.NotAvailable) return;
#if UNITY_WEBGL
            submitting = true;
            int score = data.bestScore;
            Bridge.leaderboards.SetScore(Id, score, success =>
            {
                submitting = false;
                PlaygamaBridgeAdapter.Analytics("leaderboard_submit", new Dictionary<string, object> { { "score", score }, { "ok", success } });
                if (success && SaveService.Data.bestScore == score) { SaveService.Data.bestScorePending = false; SaveService.Save(); }
            });
#endif
        }

        static string Pick(Dictionary<string, string> row, params string[] keys)
        {
            foreach (var key in keys) if (row.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)) return value;
            return null;
        }
        static int ParseInt(Dictionary<string, string> row, string key, int fallback) =>
            row.TryGetValue(key, out var v) && int.TryParse(v, out int n) ? n : fallback;
    }
}
