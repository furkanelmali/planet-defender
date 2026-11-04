using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PlanetDefender
{
    // GDD §14 state machine: BOOT → MENU → RUNNING ↔ LEVEL_UP → RUNNING → REVIVE_OFFER → RUNNING | GAME_OVER → MENU.
    // PAUSED and ads freeze whatever state they interrupt.
    public enum GameState { Boot, Menu, Shop, Running, LevelUp, Paused, ReviveOffer, GameOver }

    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] GameAssets assets;
        [SerializeField] Camera mainCamera;

        public GameConfig Config { get; private set; }
        public GameAssets Assets => assets;
        public Arena Arena { get; private set; }
        public UIController UI { get; private set; }
        public VfxService Vfx { get; private set; }
        public CameraJuice Juice { get; private set; }
        public GameState State { get; private set; } = GameState.Boot;
        public RunState Run => Arena != null ? Arena.Run : null;
        public RunState LastRun { get; private set; }
        public Settlement LastSettlement { get; private set; }
        public List<UpgradeOffer> Offers { get; private set; }

        static readonly Vector3 MenuPose = new Vector3(0, 0.35f, 0), ShopPose = new Vector3(0, 1.25f, 0);
        const float MenuScale = 1.55f;

        System.Random rng;
        bool countingDown, busy, levelUpQueued, tutorialRun, tutorialGoldShown, tutorialShotHidden;
        bool firstRun, awaitingFirstPress, firstKillSent, firstGoldSent, firstLevelSent, bestAnnounced, pendingBreakAd, doubleGoldUsed;
        float bootReadyAt;
        Transform background;
        float lastAspect;
        GameState stateBeforePause;

        // Nobody has finished a run on this save and none is waiting to be resumed: this player goes straight into play.
        public bool IsNewPlayer => string.IsNullOrEmpty(SaveService.Data.lastSettledRunId) && !HasResumableRun;
        public bool FirstRun => firstRun;
        public bool AwaitingFirstPress => awaitingFirstPress;
        public bool DoubleGoldAvailable => !doubleGoldUsed && LastSettlement.banked >= Config.ads.doubleGoldMin && PlaygamaBridgeAdapter.RewardedSupported;
        public bool RewardedAvailable => PlaygamaBridgeAdapter.RewardedSupported;

        public bool HasResumableRun
        {
            get
            {
                var d = SaveService.Data;
                return d.hasSnapshot && d.runSnapshot != null && !d.runSnapshot.runSettled
                       && !string.IsNullOrEmpty(d.runSnapshot.runId) && d.runSnapshot.runId != d.lastSettledRunId;
            }
        }

        void Awake()
        {
            // No recycling: several systems keep Tween references (time scale, flashes, counters) and kill them later;
            // with recycling a stale reference can point at an unrelated new tween (it once killed a screen's fade-out).
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly).SetCapacity(400, 100);
            Application.targetFrameRate = 60;
            Config = GameConfig.Load();
            if (assets == null) assets = GameAssets.Load();
            if (mainCamera == null) mainCamera = Camera.main;
            rng = new System.Random(Environment.TickCount);
        }

        IEnumerator Start()
        {
            BuildWorld();
            UI.Loading.Show();
            PlaygamaBridgeAdapter.Initialize();
            yield return SaveService.Load();
            var save = SaveService.Data;
            // Playgama 3.4.2: the player's own pick (flag button) wins; otherwise start in bridge.platform.language when the
            // game supports it; Loc falls back to English for anything else.
            Loc.SetLanguage(string.IsNullOrEmpty(save.settings.language) ? PlaygamaBridgeAdapter.Language : save.settings.language);
            AudioService.Create(assets.audio);
            UI.Rebuild();
            Loc.Changed += UI.Rebuild;
            PlaygamaBridgeAdapter.ExternalPauseChanged += OnExternalPause;
            LeaderboardService.SubmitPendingBest();
            PlaygamaBridgeAdapter.FirstInterstitialAfter = Config.ads.firstInterstitialAfter;
            PlaygamaBridgeAdapter.InterstitialCooldown = Config.ads.interstitialCooldown;
            // game_ready must go out whatever happens to the first screen: a failure here falls back to the menu.
            try
            {
                MissionService.Init(Config);
                MissionService.Completed += OnMissionCompleted;
                bool newPlayer = IsNewPlayer;
                PlaygamaBridgeAdapter.Analytics("session_start", new Dictionary<string, object>
                    { { "planet", save.selectedPlanet }, { "bank", save.bankGold }, { "new_player", newPlayer }, { "mobile", PlaygamaBridgeAdapter.IsMobile } });
                // A new player's first screen is the game itself, not a menu with six choices.
                if (newPlayer) StartRun(RunState.Fresh(Config.Planet(save.selectedPlanet)), false);
                else EnterMenu();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                try { awaitingFirstPress = false; EnterMenu(); } catch (Exception inner) { Debug.LogException(inner); }
            }
            // game_ready only once the first interactive frame is on screen.
            yield return null;
            yield return new WaitForEndOfFrame();
            PlaygamaBridgeAdapter.SendGameReady();
            bootReadyAt = Time.realtimeSinceStartup;
        }

        void BuildWorld()
        {
            var juiceGo = new GameObject("CameraJuice");
            Juice = juiceGo.AddComponent<CameraJuice>();
            Juice.Init(mainCamera, assets.volumeProfile);
            background = new GameObject("Starfield").transform;
            var bg = background.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = assets.quad; bg.sharedMaterial = assets.starfield; bg.sortingOrder = -100;
            Vfx = new GameObject("Vfx").AddComponent<VfxService>();
            Vfx.Init(assets.font);
            Arena = new GameObject("Arena").AddComponent<Arena>();
            Arena.Init(Config, assets, Vfx, Juice);
            Arena.LevelReady += () => { if (State == GameState.Running && !countingDown) levelUpQueued = true; };
            Arena.PlanetDestroyed += OnPlanetDestroyed;
            Arena.PlanetDamaged += OnPlanetDamaged;
            Arena.GoldLanded += OnGoldLanded;
            Arena.ComboStep += c => UI.Hud.ComboPop(c);
            Arena.GoldSpawned += OnGoldSpawned;
            Arena.MeteorKilled += OnMeteorKilled;
            var uiGo = new GameObject("UI");
            UI = uiGo.AddComponent<UIController>();
            UI.Init(this, mainCamera);
            FrameCamera(true);
        }

        void Update()
        {
            TimeController.Tick();
            SaveService.Tick();
            FrameCamera(false);
            if (State == GameState.Running && !countingDown)
            {
                // A new player's run holds still until their first press, so the opening cannot be missed or lost.
                if (awaitingFirstPress && Arena.Firing) OnFirstPress();
                if (!awaitingFirstPress)
                {
                    Arena.Tick(Time.deltaTime);
                    if (Run != null)
                    {
                        MissionService.Tick(Run);
                        TickSectors();
                        TickBestScore();
                    }
                    // A death in the same frame wins over a queued level-up.
                    if (levelUpQueued && State == GameState.Running) { levelUpQueued = false; BeginLevelUp(); }
                    else if (Run != null && Run.elapsed > 200f) AudioService.Instance?.PlayMusic(Music.Pressure, 2f);
                }
            }
            // P only: Esc belongs to the browser (leaves full screen), Playgama 1.5.2.
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (State == GameState.Running && !countingDown) Pause();
                else if (State == GameState.Paused && !UI.Settings.Visible) Resume();
            }
        }

        // Portrait keeps ~7.6 units across; landscape keeps the full height. Planet sits a little below centre.
        void FrameCamera(bool force)
        {
            float aspect = mainCamera.aspect;
            if (!force && Mathf.Approximately(aspect, lastAspect)) return;
            lastAspect = aspect;
            float size = Mathf.Max(3.8f / aspect, 6.2f);
            Juice.SetBaseSize(size);
            background.position = new Vector3(0, 0, 5);
            background.localScale = new Vector3(size * 2f * aspect + 4f, size * 2f + 4f, 1);
        }

        // ------------------------------------------------------------------ menu & shop

        public void EnterMenu()
        {
            State = GameState.Menu;
            TimeController.ResetAll();
            Juice.ResetEffects();
            Arena.EndRun();
            Arena.ClearField();
            Arena.ShowPlanet(Config.Planet(SaveService.Data.selectedPlanet), false);
            MovePlanet(MenuPose, MenuScale, 0.6f);
            UI.HideAll();
            UI.Menu.Show();
            AudioService.Instance?.PlayMusic(Music.Menu);
            LeaderboardService.SubmitPendingBest();
            if (DailyService.Pending) UI.Daily.Show();
        }

        void MovePlanet(Vector3 position, float scale, float duration)
        {
            var t = Arena.Planet.transform;
            t.DOKill();
            if (duration <= 0) { t.position = position; t.localScale = Vector3.one * scale; return; }
            t.DOMove(position, duration).SetEase(Ease.OutCubic).SetUpdate(true);
            t.DOScale(scale, duration).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);
        }

        public void OpenShop()
        {
            State = GameState.Shop;
            UI.HideAll();
            MovePlanet(ShopPose, MenuScale, 0.45f);
            UI.Shop.Show();
        }

        public void BackToMenu() { UI.Shop.Hide(); EnterMenu(); }

        public void PreviewPlanet(PlanetConfig planet, int direction)
        {
            Arena.ShowPlanet(planet, false);
            var t = Arena.Planet.transform;
            t.DOKill();
            t.position = ShopPose + new Vector3(direction * 2.5f, 0, 0);
            t.localScale = Vector3.one * (direction == 0 ? MenuScale : MenuScale * 0.6f);
            t.DOMove(ShopPose, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);
            t.DOScale(MenuScale, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public bool BuyPlanet(PlanetConfig planet)
        {
            if (!EconomyService.BuyPlanet(planet)) return false;
            AudioService.Instance?.Play(Sfx.Unlock);
            Vfx.Play(assets.levelUpBurst, ShopPose, 1.6f, planet.AtmosphereColor);
            Juice.BloomPulse(1f, 0.6f);
            Arena.Planet.Pulse(0.15f);
            return true;
        }

        public void SelectPlanet(string id)
        {
            EconomyService.SelectPlanet(id);
            AudioService.Instance?.Play(Sfx.Purchase, 0.6f);
            Arena.Planet.Pulse(0.1f);
        }

        public void OpenLanguage() => UI.Language.Show();
        public void OpenGoals() => UI.Goals.Show();

        public void ClaimDaily()
        {
            if (busy || !DailyService.Pending) return;
            GrantDaily(false);
        }

        public void WatchDailyDouble()
        {
            if (busy || !DailyService.Pending) return;
            ShowRewarded(PlaygamaBridgeAdapter.AdDailyDouble, granted =>
            {
                if (granted) GrantDaily(true);
                else UI.Daily.ShowAdFailed();
            });
        }

        void GrantDaily(bool doubled)
        {
            int amount = DailyService.Claim(Config, doubled);
            AudioService.Instance?.Play(Sfx.Unlock);
            Juice.BloomPulse(1f, 0.6f);
            UI.Daily.Hide();
            UI.Menu.Refresh();
            UI.Menu.CelebrateBank(amount);
        }

        // Every rewarded placement goes through here: reward only on the SDK's Rewarded state, buttons wait while it plays.
        void ShowRewarded(string placement, Action<bool> finished)
        {
            busy = true;
            bool granted = false;
            PlaygamaBridgeAdapter.Analytics("rewarded_offer_taken", new Dictionary<string, object> { { "placement", placement } });
            StartCoroutine(PlaygamaBridgeAdapter.Rewarded(placement, () => granted = true, () =>
            {
                busy = false;
                if (granted) PlaygamaBridgeAdapter.Analytics("rewarded_success", new Dictionary<string, object> { { "placement", placement } });
                finished(granted);
            }));
        }

        public void OpenSettings()
        {
            UI.Settings.Open(null);
        }

        public void OpenLeaderboard()
        {
            if (LeaderboardService.Type == LeaderboardService.Mode.NativePopup) { LeaderboardService.ShowNativePopup(); return; }
            UI.Leaderboard.Open(null);
        }

        // ------------------------------------------------------------------ run lifecycle

        public void Play()
        {
            if (busy) return;
            // The break the player skipped by going to the shop after a run is taken here, as they head back into play.
            if (pendingBreakAd) { pendingBreakAd = false; AfterBreak(PlaygamaBridgeAdapter.AdGameOver, PlayNow); return; }
            PlayNow();
        }

        void PlayNow()
        {
            // A suspended run the player chose not to continue is settled normally before a new one starts.
            if (HasResumableRun)
            {
                var old = RunState.FromSnapshot(SaveService.Data.runSnapshot, Config);
                EconomyService.Settle(old.runId, old.sessionGold, old.score);
            }
            StartRun(RunState.Fresh(Config.Planet(SaveService.Data.selectedPlanet)), false);
        }

        public void ContinueRun()
        {
            if (busy || !HasResumableRun) return;
            StartRun(RunState.FromSnapshot(SaveService.Data.runSnapshot, Config), true);
        }

        void StartRun(RunState run, bool resumed)
        {
            TimeController.ResetAll();
            Juice.ResetEffects();
            UI.HideAll();
            Arena.BeginRun(run);
            // The planet drops from its menu pose into play position with a small overshoot.
            MovePlanet((Vector3)Arena.PlanetCenter, 1f, 0f);
            Arena.Planet.transform.localScale = Vector3.one * 1.35f;
            Arena.Planet.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
            State = GameState.Running;
            UI.Hud.Show();
            AudioService.Instance?.PlayMusic(Music.Combat);
            Juice.LowHealth(run.hp == 1);
            PlaygamaBridgeAdapter.Run(PlaygamaBridgeAdapter.RunMessage.Started);
            PlaygamaBridgeAdapter.Analytics("run_start", new Dictionary<string, object> { { "planet", run.planet.id }, { "resumed", resumed } });
            tutorialRun = !SaveService.Data.tutorialDone && !resumed;
            tutorialGoldShown = false; tutorialShotHidden = false;
            firstRun = !resumed && IsNewPlayerForRun;
            awaitingFirstPress = firstRun;
            firstKillSent = firstGoldSent = firstLevelSent = bestAnnounced = false;
            doubleGoldUsed = false;
            pendingBreakAd = false;
            Arena.AssistUntil = firstRun ? Config.firstRun.assistSeconds : 0f;
            MissionService.BeginRun(run);
            if (!firstRun) PlaygamaBridgeAdapter.NotePlayStarted();
            if (firstRun)
            {
                Arena.SpawnIntroMeteor();
                UI.Hud.PointAt(() => Arena.IntroMeteorPosition + Vector2.up * 0.55f, Loc.T("hint_intro"), Palette.Gold);
            }
            if (resumed) StartCountdown(() => Arena.InputEnabled = true);
            else
            {
                Arena.InputEnabled = true;
                if (tutorialRun)
                {
                    bool mobile = PlaygamaBridgeAdapter.IsMobile;
                    UI.Hud.ShowHint(Loc.T(mobile ? "hint_shoot" : "hint_shoot_pc"), Loc.T(mobile ? "hint_shoot_body" : "hint_shoot_pc_body"));
                }
            }
        }

        // Asked before the run's id is settled anywhere: nothing has been banked yet on this save.
        bool IsNewPlayerForRun => string.IsNullOrEmpty(SaveService.Data.lastSettledRunId);

        void OnFirstPress()
        {
            awaitingFirstPress = false;
            PlaygamaBridgeAdapter.NotePlayStarted();
            float waited = bootReadyAt > 0 ? Time.realtimeSinceStartup - bootReadyAt : 0f;
            PlaygamaBridgeAdapter.Analytics("first_input", new Dictionary<string, object> { { "seconds", Mathf.RoundToInt(waited) }, { "mobile", PlaygamaBridgeAdapter.IsMobile } });
        }

        // ------------------------------------------------------------------ goals during a run

        void TickSectors()
        {
            while (State == GameState.Running && Run.elapsed >= Config.SectorTime(Run.sectorsPassed))
            {
                int index = Run.sectorsPassed++;
                int bonus = Config.SectorBonus(index);
                Run.sessionGold += bonus;
                Run.goldEarned += bonus;
                string threat = Config.ThreatAt(index);
                string body = "+" + UIKit.Num(bonus) + (threat != null ? "  ·  " + Loc.T("threat_" + threat) : "");
                UI.Hud.Announce(Loc.F("sector_title", index + 2), body, Palette.Plasma);
                UI.Hud.PunchGold();
                AudioService.Instance?.Play(Sfx.Unlock, 0.7f);
                Juice.BloomPulse(0.7f, 0.5f);
                if (index >= Config.sectors.bigFrom) Arena.SpawnSectorMeteor();
                PlaygamaBridgeAdapter.Analytics("sector_reached", new Dictionary<string, object>
                    { { "sector", index + 2 }, { "elapsed", Mathf.RoundToInt(Run.elapsed) }, { "first_run", firstRun }, { "level", Run.level } });
            }
        }

        void TickBestScore()
        {
            int best = SaveService.Data.bestScore;
            if (bestAnnounced || best <= 0 || Run.score <= best) return;
            bestAnnounced = true;
            UI.Hud.Announce(Loc.T("new_best"), "", Palette.Gold);
            AudioService.Instance?.Play(Sfx.NewBest, 0.7f);
        }

        void OnMissionCompleted(ActiveMission mission)
        {
            UI.Hud.Announce(Loc.T("mission_done"), $"{mission.Title}  +{UIKit.Num(mission.reward)}", Palette.Gold);
            UI.Hud.PunchGold();
            AudioService.Instance?.Play(Sfx.Purchase, 0.8f);
        }

        void StartCountdown(Action then)
        {
            countingDown = true;
            Arena.InputEnabled = false;
            TimeController.Freeze();
            UI.Countdown.Run(() =>
            {
                countingDown = false;
                TimeController.Unfreeze(0.15f);
                then?.Invoke();
            });
        }

        void SaveSnapshot()
        {
            if (Run == null || Run.settled) return;
            var d = SaveService.Data;
            d.hasSnapshot = true;
            d.runSnapshot = Run.ToSnapshot();
            SaveService.Save();
        }

        // ------------------------------------------------------------------ level-up

        void BeginLevelUp()
        {
            if (State != GameState.Running && State != GameState.LevelUp) return;
            var run = Run;
            State = GameState.LevelUp;
            Arena.InputEnabled = false;
            run.xp -= run.XpNeeded;
            run.level++;
            TimeController.Freeze(0.22f);
            AudioService.Instance?.Duck(true);
            AudioService.Instance?.Play(Sfx.LevelUp);
            Vfx.Play(assets.levelUpBurst, (Vector3)Arena.PlanetCenter, 1.2f, Palette.Xp);
            Juice.BloomPulse(1.4f, 0.7f);
            Juice.Kick(0.2f, 0.35f);
            Arena.Planet.Pulse(0.12f);
            PlaygamaBridgeAdapter.Analytics("level_up", new Dictionary<string, object> { { "level", run.level }, { "elapsed", Mathf.RoundToInt(run.elapsed) } });
            if (!firstLevelSent)
            {
                firstLevelSent = true;
                PlaygamaBridgeAdapter.Analytics("first_level_up", new Dictionary<string, object> { { "elapsed", Mathf.RoundToInt(run.elapsed) }, { "first_run", firstRun } });
            }
            SaveSnapshot();
            UI.Hud.HideHint();
            // The reward comes first; the interstitial waits until the player leaves the cards (CloseLevelUp).
            Offers = UpgradeShop.Roll(Config, run, rng, 3, firstRun && run.level == 2);
            UI.LevelUp.Open(run, Offers);
        }

        // One card per level-up can be taken for an ad when the player cannot afford it.
        public void WatchUpgradeAd(UpgradeOffer offer, Action<bool> finished)
        {
            if (State != GameState.LevelUp || busy) { finished(false); return; }
            ShowRewarded(PlaygamaBridgeAdapter.AdUpgradeFree, granted =>
            {
                if (State != GameState.LevelUp) return;
                if (granted) { offer.price = 0; BuyUpgrade(offer); }
                finished(granted);
            });
        }

        public bool BuyUpgrade(UpgradeOffer offer)
        {
            if (State != GameState.LevelUp || !UpgradeShop.Buy(Run, offer)) return false;
            Arena.RecomputeStats();
            AudioService.Instance?.Play(Sfx.Purchase);
            UI.Hud.SetGold(Run.sessionGold);
            return true;
        }

        public void SkipUpgrade() { if (!busy) CloseLevelUp(); }

        public void CloseLevelUp()
        {
            if (State != GameState.LevelUp) return;
            UI.LevelUp.Hide();
            if (Run.xp >= Run.XpNeeded) { DOVirtual.DelayedCall(0.2f, () => { if (State == GameState.LevelUp) BeginLevelUp(); }, true); return; }
            SaveSnapshot();
            // Natural break, after the reward. Bridge and our own pacing decide whether an ad actually shows.
            busy = true;
            StartCoroutine(PlaygamaBridgeAdapter.Interstitial(PlaygamaBridgeAdapter.AdLevelUp, () =>
            {
                busy = false;
                if (State != GameState.LevelUp) return;
                State = GameState.Running;
                AudioService.Instance?.Duck(false);
                // Back from an ad the player needs a moment to find the action again.
                if (PlaygamaBridgeAdapter.LastInterstitialOpened) { StartCountdown(() => Arena.InputEnabled = true); return; }
                TimeController.Unfreeze(0.3f);
                Arena.InputEnabled = true;
            }));
        }

        // ------------------------------------------------------------------ pause

        public void Pause()
        {
            if (State != GameState.Running || countingDown) return;
            stateBeforePause = State;
            State = GameState.Paused;
            Arena.InputEnabled = false;
            TimeController.Freeze();
            AudioService.Instance?.Duck(true);
            UI.Pause.Show();
            SaveSnapshot();
            PlaygamaBridgeAdapter.Run(PlaygamaBridgeAdapter.RunMessage.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            UI.Pause.Hide();
            UI.Settings.Hide();
            State = stateBeforePause;
            AudioService.Instance?.Duck(false);
            PlaygamaBridgeAdapter.Run(PlaygamaBridgeAdapter.RunMessage.Resumed);
            StartCountdown(() => Arena.InputEnabled = true);
        }

        public void EndRunFromPause()
        {
            if (State != GameState.Paused) return;
            UI.Pause.Hide();
            FinishRun();
        }

        void OnExternalPause(bool paused)
        {
            // Planned ads run from frozen states, so an ad during live play can only be one that opened after its request timed out.
            if (paused && CanAutoPause) Pause();
        }

        // Before the first press nothing moves, so a lost focus has nothing to protect and must not add a pause screen.
        bool CanAutoPause => State == GameState.Running && !countingDown && !awaitingFirstPress;

        void OnApplicationFocus(bool focused)
        {
            PlaygamaBridgeAdapter.SetFocus(focused);
            if (!focused && CanAutoPause && !Application.isEditor) Pause();
        }

        void OnApplicationPause(bool paused) { if (paused && CanAutoPause) Pause(); }

        // ------------------------------------------------------------------ damage, death, revive

        void OnPlanetDamaged()
        {
            Juice.LowHealth(Run.hp == 1);
        }

        void OnPlanetDestroyed()
        {
            if (State != GameState.Running) return;
            levelUpQueued = false;
            State = GameState.ReviveOffer;
            Arena.InputEnabled = false;
            Juice.LowHealth(false);
            Vfx.Play(assets.explosionBig, (Vector3)Arena.PlanetCenter, 2.2f);
            Vfx.Play(assets.shockwave, (Vector3)Arena.PlanetCenter, 2.5f, Palette.Danger);
            Juice.Shake(0.6f, 0.8f);
            Juice.Chroma(1f, 0.8f);
            AudioService.Instance?.Play(Sfx.GameOver);
            AudioService.Instance?.Duck(true);
            // No sudden cut: motion slows to a stop, then the revive offer or results appear (GDD §9).
            TimeController.Freeze(0.7f);
            bool offer = !Run.reviveUsed && PlaygamaBridgeAdapter.RewardedSupported;
            if (offer) PlaygamaBridgeAdapter.Analytics("rewarded_offer", new Dictionary<string, object> { { "placement", PlaygamaBridgeAdapter.AdRevive }, { "level", Run.level } });
            DOVirtual.DelayedCall(0.8f, () =>
            {
                if (State != GameState.ReviveOffer) return;
                if (offer) UI.Revive.Show(); else FinishRun();
            }, true);
        }

        public void WatchRevive()
        {
            if (State != GameState.ReviveOffer || busy) return;
            UI.Revive.SetBusy(true);
            ShowRewarded(PlaygamaBridgeAdapter.AdRevive, granted =>
            {
                UI.Revive.SetBusy(false);
                if (State != GameState.ReviveOffer) return;
                if (!granted) { UI.Revive.ShowAdFailed(); return; }   // the choice stays with the player: try again or end the run
                UI.Revive.Hide();
                DoRevive();
            });
        }

        void DoRevive()
        {
            var run = Run;
            run.reviveUsed = true;
            run.hp = 1;
            Arena.ReviveClear(Config.player.reviveClearRadius);
            Arena.Planet.Pulse(0.25f);
            AudioService.Instance?.Play(Sfx.Revive);
            AudioService.Instance?.Duck(false);
            SaveSnapshot();
            State = GameState.Running;
            StartCountdown(() =>
            {
                Arena.InputEnabled = true;
                Arena.GrantInvulnerability(Config.player.reviveInvulnerability);
                Juice.LowHealth(true);
            });
        }

        public void DeclineRevive()
        {
            if (State != GameState.ReviveOffer || busy) return;
            PlaygamaBridgeAdapter.Analytics("revive_decline", new Dictionary<string, object> { { "level", Run.level }, { "elapsed", Mathf.RoundToInt(Run.elapsed) } });
            UI.Revive.Hide();
            FinishRun();
        }

        // Settlement happens exactly once per run; the results screen shows what it did.
        void FinishRun()
        {
            var run = Run;
            if (run == null || run.settled) return;
            MissionService.Tick(run);   // the final score and time can still finish a mission; its gold is banked below
            SaveService.Data.bestSeconds = Mathf.Max(SaveService.Data.bestSeconds, Mathf.FloorToInt(run.elapsed));
            LastSettlement = EconomyService.Settle(run.runId, run.sessionGold, run.score);
            run.settled = true;
            LastRun = run;
            State = GameState.GameOver;
            Arena.InputEnabled = false;
            TimeController.Freeze();
            Juice.LowHealth(false);
            AudioService.Instance?.Duck(false);
            AudioService.Instance?.PlayMusic(Music.Menu, 1.2f);
            LeaderboardService.SubmitPendingBest();
            PlaygamaBridgeAdapter.Run(PlaygamaBridgeAdapter.RunMessage.Ended);
            PlaygamaBridgeAdapter.Analytics("run_end", new Dictionary<string, object>
            {
                { "planet", run.planet.id }, { "score", run.score }, { "level", run.level }, { "elapsed", Mathf.RoundToInt(run.elapsed) },
                { "gold_earned", run.goldEarned }, { "gold_spent", run.goldSpent }, { "kills", run.kills }, { "revived", run.reviveUsed },
                { "sector", run.sectorsPassed + 1 }, { "first_run", firstRun }, { "missions", MissionService.CompletedThisRun }, { "mobile", PlaygamaBridgeAdapter.IsMobile }
            });
            if (SaveService.Data.tutorialDone == false && run.goldEarned > 0) { SaveService.Data.tutorialDone = true; SaveService.Save(); }
            UI.HideAll(true);
            UI.GameOver.Open(run, LastSettlement);
        }

        // Results-screen actions: request an interstitial at the break, then continue whether or not one showed.
        void AfterBreak(string placement, Action next)
        {
            if (busy) return;
            busy = true;
            StartCoroutine(PlaygamaBridgeAdapter.Interstitial(placement, () => { busy = false; next(); }));
        }

        public void Retry() => AfterBreak(PlaygamaBridgeAdapter.AdGameOver, () => StartRun(RunState.Fresh(Config.Planet(SaveService.Data.selectedPlanet)), false));
        public void GameOverToMenu() => AfterBreak(PlaygamaBridgeAdapter.AdMainMenu, EnterMenu);

        // Going to spend gold is the behaviour the game wants most, so no ad stands in its way: the break moves to the next Play.
        public void GameOverToShop()
        {
            if (busy) return;
            EnterMenu();
            OpenShop();
            pendingBreakAd = true;
        }

        public void WatchDoubleGold()
        {
            if (busy || State != GameState.GameOver || !DoubleGoldAvailable) return;
            int amount = LastSettlement.banked;
            UI.GameOver.SetDoubleBusy(true);
            ShowRewarded(PlaygamaBridgeAdapter.AdDoubleGold, granted =>
            {
                UI.GameOver.SetDoubleBusy(false);
                if (!granted) { UI.GameOver.ShowAdFailed(); return; }
                doubleGoldUsed = true;
                EconomyService.AddBank(amount);
                AudioService.Instance?.Play(Sfx.Unlock);
                UI.GameOver.ShowDoubled(amount);
            });
        }

        // ------------------------------------------------------------------ juice hooks

        void OnGoldLanded(Vector3 at, int amount)
        {
            UI.FlyCoin(at, UI.Hud.PunchGold);
            if (!firstGoldSent)
            {
                firstGoldSent = true;
                PlaygamaBridgeAdapter.Analytics("first_gold", new Dictionary<string, object> { { "elapsed", Mathf.RoundToInt(Run.elapsed) }, { "first_run", firstRun } });
            }
            if (tutorialRun && !SaveService.Data.tutorialDone)
            {
                SaveService.Data.tutorialDone = true;
                SaveService.Save();
                UI.Hud.HideHint();
            }
        }

        void OnGoldSpawned(GoldPickup g)
        {
            if (!tutorialRun || tutorialGoldShown) return;
            tutorialGoldShown = true;
            UI.Hud.PointAtGold(g);
            UI.Hud.ShowHint(Loc.T("hint_gold"), Loc.T("hint_gold_body"), 5f);
            TimeController.SlowMotion(0.35f, 1.3f, 0.5f);
            AudioService.Instance?.Play(Sfx.Countdown, 0.6f, 1.4f);
        }

        void OnMeteorKilled()
        {
            if (!firstKillSent)
            {
                firstKillSent = true;
                PlaygamaBridgeAdapter.Analytics("first_kill", new Dictionary<string, object> { { "elapsed", Mathf.RoundToInt(Run.elapsed) }, { "first_run", firstRun } });
            }
            if (!tutorialRun || tutorialShotHidden || tutorialGoldShown) return;
            if (Arena.ShotsFired >= 4) { tutorialShotHidden = true; UI.Hud.HideHint(); }
        }

        void OnDestroy()
        {
            PlaygamaBridgeAdapter.ExternalPauseChanged -= OnExternalPause;
            MissionService.Completed -= OnMissionCompleted;
            if (UI != null) Loc.Changed -= UI.Rebuild;
            TimeController.ResetAll();
            DOTween.KillAll();
        }
    }
}
