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
        Transform background;
        float lastAspect;
        GameState stateBeforePause;

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
            PlaygamaBridgeAdapter.Analytics("session_start", new Dictionary<string, object> { { "planet", save.selectedPlanet }, { "bank", save.bankGold } });
            EnterMenu();
            // game_ready only once the first interactive menu frame is on screen.
            yield return null;
            yield return new WaitForEndOfFrame();
            PlaygamaBridgeAdapter.SendGameReady();
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
                Arena.Tick(Time.deltaTime);
                // A death in the same frame wins over a queued level-up.
                if (levelUpQueued && State == GameState.Running) { levelUpQueued = false; BeginLevelUp(); }
                else if (Run != null && Run.elapsed > 200f) AudioService.Instance?.PlayMusic(Music.Pressure, 2f);
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
            if (resumed) StartCountdown(() => Arena.InputEnabled = true);
            else
            {
                Arena.InputEnabled = true;
                if (tutorialRun) UI.Hud.ShowHint(Loc.T("hint_shoot"), Loc.T("hint_shoot_body"));
            }
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
            SaveSnapshot();
            busy = true;
            // Natural break: Bridge decides whether an interstitial actually shows; the cards follow either way.
            StartCoroutine(PlaygamaBridgeAdapter.Interstitial(PlaygamaBridgeAdapter.AdLevelUp, () =>
            {
                busy = false;
                if (State != GameState.LevelUp) return;
                Offers = UpgradeShop.Roll(Config, run, rng);
                UI.LevelUp.Open(run, Offers);
            }));
        }

        public bool BuyUpgrade(UpgradeOffer offer)
        {
            if (State != GameState.LevelUp || !UpgradeShop.Buy(Run, offer)) return false;
            Arena.RecomputeStats();
            AudioService.Instance?.Play(Sfx.Purchase);
            UI.Hud.SetGold(Run.sessionGold);
            return true;
        }

        public void SkipUpgrade() => CloseLevelUp();

        public void CloseLevelUp()
        {
            if (State != GameState.LevelUp) return;
            UI.LevelUp.Hide();
            if (Run.xp >= Run.XpNeeded) { DOVirtual.DelayedCall(0.2f, () => { if (State == GameState.LevelUp) BeginLevelUp(); }, true); return; }
            State = GameState.Running;
            AudioService.Instance?.Duck(false);
            TimeController.Unfreeze(0.3f);
            Arena.InputEnabled = true;
            SaveSnapshot();
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
            if (paused && State == GameState.Running && !countingDown && !PlaygamaBridgeAdapter.AdOpen) Pause();
        }

        void OnApplicationFocus(bool focused)
        {
            PlaygamaBridgeAdapter.SetFocus(focused);
            if (!focused && State == GameState.Running && !countingDown && !Application.isEditor) Pause();
        }

        void OnApplicationPause(bool paused) { if (paused && State == GameState.Running && !countingDown) Pause(); }

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
            if (offer) PlaygamaBridgeAdapter.Analytics("rewarded_offer", new Dictionary<string, object> { { "level", Run.level } });
            DOVirtual.DelayedCall(0.8f, () =>
            {
                if (State != GameState.ReviveOffer) return;
                if (offer) UI.Revive.Show(); else FinishRun();
            }, true);
        }

        public void WatchRevive()
        {
            if (State != GameState.ReviveOffer || busy) return;
            busy = true;
            UI.Revive.SetBusy(true);
            bool granted = false;
            StartCoroutine(PlaygamaBridgeAdapter.Rewarded(PlaygamaBridgeAdapter.AdRevive, () => granted = true, () =>
            {
                busy = false;
                UI.Revive.SetBusy(false);
                if (State != GameState.ReviveOffer) return;
                UI.Revive.Hide();
                if (granted) DoRevive(); else FinishRun();
            }));
        }

        void DoRevive()
        {
            var run = Run;
            run.reviveUsed = true;
            run.hp = 1;
            PlaygamaBridgeAdapter.Analytics("rewarded_success", new Dictionary<string, object> { { "level", run.level } });
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
            UI.Revive.Hide();
            FinishRun();
        }

        // Settlement happens exactly once per run; the results screen shows what it did.
        void FinishRun()
        {
            var run = Run;
            if (run == null || run.settled) return;
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
                { "gold_earned", run.goldEarned }, { "gold_spent", run.goldSpent }, { "kills", run.kills }, { "revived", run.reviveUsed }
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
        public void GameOverToShop() => AfterBreak(PlaygamaBridgeAdapter.AdGameOver, () => { EnterMenu(); OpenShop(); });

        // ------------------------------------------------------------------ juice hooks

        void OnGoldLanded(Vector3 at, int amount)
        {
            UI.FlyCoin(at, UI.Hud.PunchGold);
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
            if (!tutorialRun || tutorialShotHidden || tutorialGoldShown) return;
            if (Arena.ShotsFired >= 4) { tutorialShotHidden = true; UI.Hud.HideHint(); }
        }

        void OnDestroy()
        {
            PlaygamaBridgeAdapter.ExternalPauseChanged -= OnExternalPause;
            if (UI != null) Loc.Changed -= UI.Rebuild;
            TimeController.ResetAll();
            DOTween.KillAll();
        }
    }
}
