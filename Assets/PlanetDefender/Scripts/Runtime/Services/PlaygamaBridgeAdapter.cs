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
        public const string AdLevelUp = "level_up", AdGameOver = "game_over", AdMainMenu = "main_menu", AdRevive = "revive";

        static bool gameReadySent, platformPaused, adOpen, focusLost;
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
#else
            Language = Application.systemLanguage == SystemLanguage.Turkish ? "tr" : "en";
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

        public static void SendGameReady()
        {
            if (gameReadySent) return;
            gameReadySent = true;
#if UNITY_WEBGL
            Bridge.platform.SendMessage(PlatformMessage.GameReady);
#endif
        }

        public enum RunMessage { Started, Paused, Resumed, Ended }
        // Platform lifecycle messages for a run (LevelStarted/Paused/Resumed/Failed in Bridge v2.4).
        public static void Run(RunMessage message)
        {
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

        static bool adBusy;
        // Batch play-mode checks cannot click the Bridge editor ad window; their ad requests complete at once.
        public static bool SkipAdsForTests;
        // Bridge decides whether an interstitial actually shows (frequency rules); done fires exactly once either way.
        public static IEnumerator Interstitial(string placement, Action done)
        {
#if UNITY_WEBGL
            if (SkipAdsForTests || adBusy || !Bridge.advertisement.isInterstitialSupported) { done?.Invoke(); yield break; }
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
            if (opened) Analytics("interstitial_shown", new Dictionary<string, object> { { "placement", placement } });
            SetAdOpen(false); adBusy = false;
#endif
            done?.Invoke();
            yield break;
        }

        // rewarded fires at most once and only on the SDK's Rewarded state; done always fires once afterwards.
        public static IEnumerator Rewarded(string placement, Action rewarded, Action done)
        {
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
            SetAdOpen(false); adBusy = false;
#endif
            done?.Invoke();
            yield break;
        }
    }
}
