using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
using Playgama.Modules.Platform;
#endif

namespace PlanetDefender
{
    // The only gameplay-facing entry to Playgama Bridge v2. Non-web builds compile pass-through branches so the
    // menu and gameplay never depend on the SDK being present (GDD §14, "SDK kullanılabilir değil").
    public static class PlaygamaBridgeAdapter
    {
#if UNITY_WEBGL
        public const bool Available = true;
#else
        public const bool Available = false;
#endif
        public const string AdLevelUp = "level_up", AdGameOver = "game_over", AdMainMenu = "main_menu", AdRevive = "revive",
            AdDoubleGold = "double_gold", AdUpgradeFree = "upgrade_free", AdDailyDouble = "daily_double";

        static bool gameReadySent, platformPaused, adOpen, focusLost, runStartedBeforeReady;
        public static string Language { get; private set; } = "en";
        public static bool PlatformAudioEnabled { get; private set; } = true;
        public static bool ExternalPause => platformPaused || adOpen;
        public static bool AdOpen => adOpen;
        public static event Action<bool> ExternalPauseChanged;
        public static event Action AudioStateChanged;

#if UNITY_WEBGL
        // Bridge events start flowing as soon as the player loads; the receiver must exist before the first scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateReceiver() { _ = Bridge.platform; }
#endif

        public static void Initialize()
        {
#if UNITY_WEBGL
            Language = string.IsNullOrEmpty(Bridge.platform.language) ? "en" : Bridge.platform.language;
            PlatformAudioEnabled = Bridge.platform.isAudioEnabled;
            Bridge.platform.audioStateChanged += enabled => { PlatformAudioEnabled = enabled; ApplyAudio(); AudioStateChanged?.Invoke(); };
            Bridge.platform.pauseStateChanged += paused => SetPlatformPause(paused);
            // Always listening: an ad that opens after the request timed out must still freeze and silence the game.
            Bridge.advertisement.interstitialStateChanged += state => { if (state == InterstitialState.Opened) SetAdOpen(true); else if (state == InterstitialState.Closed || state == InterstitialState.Failed) SetAdOpen(false); };
            Bridge.advertisement.rewardedStateChanged += state => { if (state == RewardedState.Opened) SetAdOpen(true); else if (state == RewardedState.Closed || state == RewardedState.Failed) SetAdOpen(false); };
#else
            Language = "en";   // non-web builds have no platform language; never read the OS (Playgama 3.4.2)
#endif
            ApplyAudio();
        }

        public static bool IsMobile
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var type = Bridge.device.type;
                return type == Playgama.Modules.Device.DeviceType.Mobile || type == Playgama.Modules.Device.DeviceType.Tablet || Application.isMobilePlatform;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public static bool GameReadySent => gameReadySent;

        public static void SendGameReady()
        {
            if (gameReadySent) return;
            gameReadySent = true;
#if UNITY_WEBGL
            Bridge.platform.SendMessage(PlatformMessage.GameReady);
#endif
            if (runStartedBeforeReady) { runStartedBeforeReady = false; Run(RunMessage.Started); }
        }

        public enum RunMessage { Started, Paused, Resumed, Ended }
        // Platform lifecycle messages for a run (LevelStarted/Paused/Resumed/Failed in Bridge v2.4).
        public static void Run(RunMessage message)
        {
            // A first launch opens straight into a run; the platform must still hear game_ready first.
            if (message == RunMessage.Started && !gameReadySent) { runStartedBeforeReady = true; return; }
#if UNITY_WEBGL
            var kind = message == RunMessage.Started ? PlatformMessage.LevelStarted
                : message == RunMessage.Paused ? PlatformMessage.LevelPaused
                : message == RunMessage.Resumed ? PlatformMessage.LevelResumed : PlatformMessage.LevelFailed;
            Bridge.platform.SendMessage(kind);
#endif
        }

        public static void Analytics(string name, Dictionary<string, object> data = null)
        {
#if UNITY_WEBGL
            Bridge.analytics.Send(name, data);
#endif
        }

        public static void SetFocus(bool focused) { focusLost = !focused; ApplyAudio(); }
        static void SetAdOpen(bool open) { if (adOpen == open) return; bool before = ExternalPause; adOpen = open; Changed(before); }
        static void SetPlatformPause(bool paused) { if (platformPaused == paused) return; bool before = ExternalPause; platformPaused = paused; Changed(before); }
        static void Changed(bool before) { ApplyAudio(); if (before != ExternalPause) ExternalPauseChanged?.Invoke(ExternalPause); }

        // Ads, platform pause and hidden tabs silence everything; the player's own mute lives in AudioService.
        static void ApplyAudio()
        {
            AudioListener.pause = ExternalPause || (Available && focusLost);
            AudioListener.volume = PlatformAudioEnabled ? 1 : 0;
        }

        public static bool RewardedSupported
        {
            get
            {
#if UNITY_WEBGL
                return Bridge.advertisement.isRewardedSupported;
#else
                return false;
#endif
            }
        }

        // Our own interstitial pacing, on top of whatever the platform enforces. Rewarded ads are never limited here,
        // but they do count as the last ad: an opt-in ad is not followed straight away by a forced one.
        public static float FirstInterstitialAfter = 45f, InterstitialCooldown = 60f;
        static float playStartedAt = -1f, lastAdClosedAt = -1f;
        public static bool LastInterstitialOpened { get; private set; }

        public static void NotePlayStarted() { if (playStartedAt < 0) playStartedAt = Time.realtimeSinceStartup; }

        public static bool InterstitialDue(float now, float playStart, float lastClosed, float firstAfter, float cooldown)
        {
            if (playStart < 0 || now - playStart < firstAfter) return false;
            return lastClosed < 0 || now - lastClosed >= cooldown;
        }

        static bool adBusy;
        // Batch play-mode checks cannot click the Bridge editor ad window; their ad requests complete at once.
        public static bool SkipAdsForTests;
        public static bool GrantRewardedForTests;
        // Bridge decides whether an interstitial actually shows (frequency rules); done fires exactly once either way.
        public static IEnumerator Interstitial(string placement, Action done)
        {
            LastInterstitialOpened = false;
#if UNITY_WEBGL
            if (SkipAdsForTests || adBusy || !Bridge.advertisement.isInterstitialSupported
                || !InterstitialDue(Time.realtimeSinceStartup, playStartedAt, lastAdClosedAt, FirstInterstitialAfter, InterstitialCooldown)) { done?.Invoke(); yield break; }
            adBusy = true;
            Analytics("interstitial_request", new Dictionary<string, object> { { "placement", placement } });
            bool started = false, opened = false, finished = false;
            void OnState(InterstitialState state)
            {
                started = true;
                if (state == InterstitialState.Opened) { opened = true; SetAdOpen(true); }
                else if (state == InterstitialState.Closed || state == InterstitialState.Failed) finished = true;
            }
            Bridge.advertisement.interstitialStateChanged += OnState;
            Bridge.advertisement.ShowInterstitial(placement);
            float begun = Time.realtimeSinceStartup;
            while (!finished)
            {
                float waited = Time.realtimeSinceStartup - begun;
                if (!started && waited > 0.6f) break;   // no state at all: Bridge skipped this request
                if (!opened && waited > 8f) break;      // stuck load: give the player their break screen
                yield return null;
            }
            Bridge.advertisement.interstitialStateChanged -= OnState;
            if (opened)
            {
                LastInterstitialOpened = true;
                lastAdClosedAt = Time.realtimeSinceStartup;
                Analytics("interstitial_shown", new Dictionary<string, object> { { "placement", placement } });
            }
            adBusy = false;
#endif
            done?.Invoke();
            yield break;
        }

        // rewarded fires at most once and only on the SDK's Rewarded state; done always fires once afterwards.
        public static IEnumerator Rewarded(string placement, Action rewarded, Action done)
        {
            if (GrantRewardedForTests) { rewarded?.Invoke(); done?.Invoke(); yield break; }
#if UNITY_WEBGL
            if (SkipAdsForTests || adBusy || !RewardedSupported) { done?.Invoke(); yield break; }
            adBusy = true;
            bool opened = false, finished = false, granted = false;
            void OnState(RewardedState state)
            {
                if (state == RewardedState.Opened) { opened = true; SetAdOpen(true); }
                else if (state == RewardedState.Rewarded && !granted) { granted = true; rewarded?.Invoke(); }
                else if (state == RewardedState.Closed || state == RewardedState.Failed) finished = true;
            }
            Bridge.advertisement.rewardedStateChanged += OnState;
            Bridge.advertisement.ShowRewarded(placement);
            float begun = Time.realtimeSinceStartup;
            while (!finished && (opened || Time.realtimeSinceStartup - begun < 8f)) yield return null;
            Bridge.advertisement.rewardedStateChanged -= OnState;
            if (opened) lastAdClosedAt = Time.realtimeSinceStartup;
            if (!granted) Analytics("rewarded_failed", new Dictionary<string, object> { { "placement", placement }, { "opened", opened } });
            adBusy = false;
#endif
            done?.Invoke();
            yield break;
        }
    }
}
