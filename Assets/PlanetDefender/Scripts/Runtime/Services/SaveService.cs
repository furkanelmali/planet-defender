using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
#endif

namespace PlanetDefender
{
    [Serializable] public sealed class SaveSettings { public bool musicMuted; public bool sfxMuted; public string language = ""; }
    [Serializable] public sealed class UpgradeLevel { public string id; public int level; }

    // GDD §15 RunSnapshot. Written only at level-up/ad/pause breaks, never per meteor or per gold.
    [Serializable] public sealed class RunSnapshot
    {
        public string runId, planet;
        public int hp, level, xp, score, sessionGold, goldEarned, goldSpent, kills;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public bool reviveUsed, runSettled;
        public float elapsedSeconds;
    }

    // GDD §14 pd_save_v1. JsonUtility cannot hold a null object, so hasSnapshot marks a live runSnapshot.
    [Serializable] public sealed class PlayerSave
    {
        public int version = 1;
        public int bankGold;
        public List<string> unlockedPlanets = new List<string> { "earth" };
        public string selectedPlanet = "earth";
        public int bestScore;
        public bool bestScorePending;       // a new best that the leaderboard has not accepted yet
        public string lastSettledRunId = "";
        public SaveSettings settings = new SaveSettings();
        public bool tutorialDone;
        public bool hasSnapshot;
        public RunSnapshot runSnapshot = new RunSnapshot();
    }

    // Bridge Storage on the web (the only save channel there, GDD §14); PlayerPrefs only in non-web builds.
    public static class SaveService
    {
        public const string Key = "pd_save_v1";
        public static PlayerSave Data { get; private set; } = new PlayerSave();
        public static bool Loaded { get; private set; }
        public static bool LastWriteFailed { get; private set; }
        static bool writing, dirtyWhileWriting;
        static float retryAt = -1;

        public static IEnumerator Load()
        {
            string json = null;
            bool done = false;
#if UNITY_WEBGL
            Bridge.storage.Get(Key, (success, value) => { if (success) json = value; done = true; });
            float giveUp = Time.realtimeSinceStartup + 8f;  // Bridge resolves or rejects; this guards a broken host page
            while (!done && Time.realtimeSinceStartup < giveUp) yield return null;
#else
            json = PlayerPrefs.GetString(Key, null);
            done = true;
#endif
            Data = Parse(json);
            Loaded = true;
            yield break;
        }

        public static PlayerSave Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new PlayerSave();
            try
            {
                var data = JsonUtility.FromJson<PlayerSave>(json) ?? new PlayerSave();
                if (data.unlockedPlanets == null || data.unlockedPlanets.Count == 0) data.unlockedPlanets = new List<string> { "earth" };
                if (!data.unlockedPlanets.Contains("earth")) data.unlockedPlanets.Insert(0, "earth");
                if (string.IsNullOrEmpty(data.selectedPlanet) || !data.unlockedPlanets.Contains(data.selectedPlanet)) data.selectedPlanet = "earth";
                data.settings ??= new SaveSettings();
                data.runSnapshot ??= new RunSnapshot();
                data.bankGold = Mathf.Max(0, data.bankGold);
                return data;
            }
            catch (ArgumentException) { return new PlayerSave(); } // corrupt save: start fresh rather than crash the menu
        }

        // Whole document in one write, so settlement (bank gold + lastSettledRunId) is atomic (GDD §14).
        public static void Save()
        {
            if (writing) { dirtyWhileWriting = true; return; }
            string json = JsonUtility.ToJson(Data);
#if UNITY_WEBGL
            writing = true;
            Bridge.storage.Set(Key, json, success =>
            {
                writing = false;
                LastWriteFailed = !success;
                if (!success) retryAt = Time.realtimeSinceStartup + 3f;
                if (dirtyWhileWriting) { dirtyWhileWriting = false; Save(); }
            });
#else
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
#endif
        }

        // Retries a failed write from the game loop; never writes on its own otherwise.
        public static void Tick()
        {
            if (retryAt >= 0 && Time.realtimeSinceStartup >= retryAt) { retryAt = -1; Save(); }
        }

        public static void ResetForTests(PlayerSave data) { Data = data ?? new PlayerSave(); Loaded = true; }
    }
}
