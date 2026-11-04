using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlanetDefender.EditorTools
{
    // Batch play-mode check: drives the real game through menu → settings → language → shop → run → level-up (buy,
    // then Skip by a real pointer click) → pause → revive → game over, asserts the GDD rules on the way and writes one
    // contact sheet per screen showing it at four aspect ratios (tall phone, 16:9 phone, 3:4 tablet, 16:9 desktop).
    // Run without -quit:  -executeMethod PlanetDefender.EditorTools.SmokeCapture.Run   (env PD_CAPTURE_DIR = output folder)
    [InitializeOnLoad]
    public static class SmokeCapture
    {
        const string Key = "PD.Smoke.Active";
        static readonly Vector2Int[] Sizes = { new Vector2Int(585, 1266), new Vector2Int(630, 1120), new Vector2Int(840, 1120), new Vector2Int(1600, 900) };
        static int step, sub;
        static double next, deadline;
        static GameManager game;
        static string dir;
        static int savedBank, savedKills, savedGold, savedReward;
        static string savedMission;
        static readonly Dictionary<string, Vector2> menuHomes = new Dictionary<string, Vector2>();

        // Pending contact sheet: one capture per size, a little wall time between sizes so layout settles.
        static string sheetName;
        static int sheetIndex;
        static readonly List<Texture2D> sheetShots = new List<Texture2D>();

        static SmokeCapture() { EditorApplication.update += Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene(PlanetDefenderBuilder.ScenePath);
            PlayerPrefs.DeleteKey("bridge_storage_data_" + SaveService.Key);
            PlayerPrefs.DeleteKey(SaveService.Key);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception("PD_CHECK_FAIL: " + what);
            Debug.Log("PD_CHECK_OK: " + what);
        }

        static void Wait(double seconds) => next = EditorApplication.timeSinceStartup + seconds;

        static void Sheet(string name) { sheetName = name; sheetIndex = 0; sheetShots.Clear(); SetSize(Sizes[0]); Wait(1.6); }

        static void SetSize(Vector2Int size)
        {
            var cam = game.Juice.Camera;
            var old = cam.targetTexture;
            cam.targetTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            if (old != null) { old.Release(); UnityEngine.Object.DestroyImmediate(old); }
        }

        static Texture2D Grab()
        {
            var rt = game.Juice.Camera.targetTexture;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            RenderTexture.active = prev;
            return img;
        }

        static void TickSheet()
        {
            sheetShots.Add(Grab());
            sheetIndex++;
            if (sheetIndex < Sizes.Length) { SetSize(Sizes[sheetIndex]); Wait(1.6); return; }
            // Compose side by side at a common height.
            const int h = 900, gap = 24;
            int w = gap;
            foreach (var s in sheetShots) w += Mathf.RoundToInt(s.width * (h / (float)s.height)) + gap;
            var sheet = new Texture2D(w, h + gap * 2, TextureFormat.RGB24, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(40, 44, 60, 255);
            sheet.SetPixels32(fill);
            int x = gap;
            foreach (var s in sheetShots)
            {
                int sw = Mathf.RoundToInt(s.width * (h / (float)s.height));
                for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < sw; xx++)
                    sheet.SetPixel(x + xx, gap + yy, s.GetPixelBilinear((xx + 0.5f) / sw, (yy + 0.5f) / h));
                x += sw + gap;
                UnityEngine.Object.DestroyImmediate(s);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, sheetName + ".png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("PD_CAPTURE " + sheetName);
            sheetShots.Clear();
            sheetName = null;
            SetSize(Sizes[0]);
            Wait(0.8);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (deadline <= 0) deadline = EditorApplication.timeSinceStartup + 600;
            if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError($"PD_CHECK_FAIL: smoke timed out at step {step} state={(game != null ? game.State.ToString() : "none")} elapsed={(game != null && game.Run != null ? game.Run.elapsed : -1)} timeScale={Time.timeScale} frame={Time.frameCount}"); Finish(1); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (game != null && game.Run != null && step < 22) game.Run.hp = game.Run.maxHp;
                else if (game != null && game.Run != null && step >= 100 && step <= 116 && step != 102 && step != 103) game.Run.hp = game.Run.maxHp;
                if (sheetName != null) { TickSheet(); return; }
                Step();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Click(UnityEngine.UI.Button button)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        static Transform MenuColumn() => game.UI.Menu.root.Find("Column");

        static void Press(Transform target)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerUpHandler);
        }

        // A touch released while the level-up screen opens sends PointerExit to the button under the finger; that used to
        // kill Skip's entrance halfway, and each time Skip settled lower until it left the screen.
        static void LiftFingerOver(UnityEngine.UI.Button button)
        {
            var data = new PointerEventData(EventSystem.current) { pointerId = 0 };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
            Debug.Log($"PD_DEBUG finger lifted over Skip at {((RectTransform)button.transform).anchoredPosition}");
        }

        static void CheckSkipShown()
        {
            var skip = (RectTransform)game.UI.LevelUp.SkipButton.transform;
            var cg = skip.GetComponent<CanvasGroup>();
            Debug.Log($"PD_DEBUG skip at {skip.anchoredPosition} alpha {(cg ? cg.alpha : -1):0.00} scale {skip.localScale.x:0.00}");
            Check((skip.anchoredPosition - new Vector2(0, 36)).sqrMagnitude < 0.01f && cg != null && cg.alpha > 0.99f, "Skip is on screen at its layout position");
        }

        static void Step()
        {
            var run = game != null ? game.Run : null;
            switch (step)
            {
                case 0:
                    game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                    if (game == null || game.State == GameState.Boot) { Wait(0.2); return; }
                    dir = Environment.GetEnvironmentVariable("PD_CAPTURE_DIR");
                    if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("Logs/Captures");
                    Directory.CreateDirectory(dir);
                    PlaygamaBridgeAdapter.SkipAdsForTests = true;
                    Application.runInBackground = true;   // a batch editor losing window focus must not stop the player loop
                    SetSize(Sizes[0]);
                    // First launch: straight into a run, nothing moves until the first press, and the first meteor is marked.
                    Check(game.State == GameState.Running && !game.UI.Menu.Visible, "first launch opens in the game, not the menu");
                    Check(game.FirstRun && game.AwaitingFirstPress && game.Arena.AliveMeteors == 1 && game.Arena.IntroMeteorPosition.HasValue, "first run waits for the first press with a marked meteor");
                    Check(!PlaygamaBridgeAdapter.InterstitialDue(30f, 0f, -1f, 45f, 60f), "no interstitial before the first-ad floor");
                    Check(PlaygamaBridgeAdapter.InterstitialDue(50f, 0f, -1f, 45f, 60f), "interstitial allowed after the first-ad floor");
                    Check(!PlaygamaBridgeAdapter.InterstitialDue(100f, 0f, 50f, 45f, 60f) && PlaygamaBridgeAdapter.InterstitialDue(111f, 0f, 50f, 45f, 60f), "interstitial cooldown is respected");
                    Check(game.Config.SectorTime(0) == 30f && game.Config.SectorTime(1) == 60f && game.Config.SectorTime(2) == 120f, "sector schedule 30 s, then every minute");
                    Check(game.Config.ThreatAt(1) == "fast" && game.Config.ThreatAt(4) == "big" && game.Config.ThreatAt(5) == null, "sector threats follow the meteor unlocks");
                    Check(MissionService.Active().Count == 3, "three missions active on a new save");
                    step = 100; Wait(2.0); return;
                case 100:
                    // game_ready waits for WaitForEndOfFrame, which never fires in batch mode: it is measured in the WebGL build instead.
                    Check(Mathf.Approximately(game.Run.elapsed, 0f) && game.AwaitingFirstPress, "run time stands still before the first press");
                    Sheet("00a_first_launch"); step++; return;
                case 101:
                    game.Arena.DebugAim = (game.Arena.IntroMeteorPosition.Value - Arena.PlanetCenter).normalized;
                    step++; Wait(0.5); return;
                case 102:
                    if (game.AwaitingFirstPress || game.Run.kills == 0) { if (game.Run.elapsed > 20f) throw new Exception("PD_CHECK_FAIL: intro meteor was not shot"); Wait(0.2); return; }
                    Check(game.Run.elapsed > 0f && !game.Arena.IntroMeteorPosition.HasValue, "first press starts the run and the intro meteor can be shot");
                    game.Arena.DebugAim = null;
                    // First-run assist: the last life survives a hit while the assist window is open.
                    game.Run.hp = 1;
                    savedKills = game.Run.kills;
                    game.Arena.DebugSpawn("normal", (Vector2)Arena.PlanetCenter + new Vector2(0, 1.9f));
                    step++; Wait(1.0); return;
                case 103:
                    if (game.Run.elapsed < 4f) { Wait(0.2); return; }
                    Check(game.State == GameState.Running && game.Run.hp == 1, "first-run assist keeps the last life");
                    game.Run.hp = game.Run.maxHp;
                    game.Run.xp = game.Run.XpNeeded;
                    step++; Wait(0.5); return;
                case 104:
                    if (game.State != GameState.LevelUp || game.Offers == null || !game.UI.LevelUp.Visible) { Wait(0.2); return; }
                    if (sub++ < 5) { Wait(0.3); return; }
                    sub = 0;
                    Check(game.Offers.Count == 3 && game.Offers.TrueForAll(o => o.price == 0), "first level-up of a new player is free");
                    Sheet("00b_first_levelup"); step++; return;
                case 105:
                {
                    var gift = game.Offers[0];
                    int goldBefore = game.Run.sessionGold;
                    Check(game.BuyUpgrade(gift) && game.Run.sessionGold == goldBefore, "the free upgrade costs nothing");
                    game.CloseLevelUp();
                    step++; Wait(1.0); return;
                }
                case 106:
                {
                    Check(game.State == GameState.Running, "run resumes after the free upgrade");
                    // Sector milestone: time jumps to the first one; bonus paid once, banner up.
                    savedGold = game.Run.sessionGold;
                    game.Run.elapsed = 30f;
                    step++; Wait(0.6); return;
                }
                case 107:
                    if (game.Run.sectorsPassed < 1) { Wait(0.2); return; }
                    Check(game.Run.sectorsPassed == 1 && game.Run.sessionGold >= savedGold + game.Config.SectorBonus(0), "first sector milestone pays its bonus");
                    // Mission: finish the first one by hand and watch gold, tier and rotation.
                    var firstMission = MissionService.Active()[0];
                    savedGold = game.Run.sessionGold;
                    savedMission = firstMission.config.id;
                    firstMission.state.progress = firstMission.target;
                    savedReward = firstMission.reward;
                    step++; Wait(0.5); return;
                case 108:
                    if (MissionService.CompletedThisRun == 0) { Wait(0.2); return; }
                    Check(game.Run.sessionGold >= savedGold + savedReward, "a finished mission pays session gold");
                    Check(!MissionService.Active().Exists(m => m.config.id == savedMission) && MissionService.Active().Count == 3, "a finished mission is replaced from the rotation");
                    Sheet("00c_goal_banner"); step++; return;
                case 109:
                    game.Run.score = 900;
                    game.Pause();
                    step++; Wait(1.0); return;
                case 110:
                    game.EndRunFromPause();
                    step++; Wait(2.5); return;
                case 111:
                    Check(game.State == GameState.GameOver, "first run can be ended from pause");
                    Check(SaveService.Data.bestSeconds >= 30, "best survival time is saved");
                    Check(!game.IsNewPlayer, "a settled run ends the new-player state");
                    Sheet("00d_first_results"); step++; return;
                case 112:
                    game.GameOverToMenu();
                    step++; Wait(2.0); return;
                case 113:
                    Check(game.State == GameState.Menu && game.UI.Daily.Visible && DailyService.Pending, "the daily reward appears on the first menu");
                    Sheet("00e_daily"); step++; return;
                case 114:
                {
                    int bank = SaveService.Data.bankGold;
                    Click(game.UI.Daily.ClaimButton);
                    Check(SaveService.Data.bankGold == bank + game.Config.daily.rewards[0] && !DailyService.Pending && SaveService.Data.dailyStreak == 1, "claiming pays day one once");
                    Check(DailyService.Claim(game.Config, false) == 0, "the daily reward cannot be claimed twice");
                    DailyService.DayOffsetForTests = 1;
                    Check(DailyService.Pending && DailyService.NextStreak == 2 && DailyService.Reward(game.Config, 2) == game.Config.daily.rewards[1], "tomorrow continues the streak");
                    DailyService.DayOffsetForTests = 3;
                    Check(DailyService.Pending && DailyService.NextStreak == 1, "a missed day restarts the streak");
                    DailyService.DayOffsetForTests = 0;
                    game.OpenGoals();
                    step++; Wait(1.5); return;
                }
                case 115: Sheet("00f_goals"); step++; return;
                case 116:
                    game.UI.Goals.Hide(true);
                    SaveService.Data.bankGold = 0;
                    SaveService.Data.bestScore = 0;
                    step = 200; Wait(1.0); return;
                case 200:
                    Check(Loc.Language == "en", "default language is English");
                    Check(SaveService.Data.unlockedPlanets.Count == 1 && SaveService.Data.selectedPlanet == "earth", "new player owns only Earth");
                    Check(game.Config.Planet("earth").hp == 3, "Earth starts with 3 HP");
                    Check(GameConfig.XpForLevel(1) == 100 && GameConfig.XpForLevel(2) == 145 && GameConfig.XpForLevel(3) == 200 && GameConfig.XpForLevel(4) == 265, "XP curve matches GDD");
                    var rapid = game.Config.Upgrade("rapid_fire");
                    Check(game.Config.UpgradePrice(rapid, 0) == 40 && game.Config.UpgradePrice(rapid, 1) == 66, "upgrade price growth ceil(p*1.65^tier)");
                    step = 1; Wait(3.0); return;
                case 1:
                    foreach (RectTransform c in MenuColumn()) menuHomes[c.name] = c.anchoredPosition;
                    Sheet("01_menu"); step++; return;
                case 2: game.OpenSettings(); step++; Wait(1.5); return;
                case 3: Sheet("02_settings"); step++; return;
                case 4: game.UI.Settings.Hide(true); game.OpenLanguage(); step++; Wait(1.5); return;
                case 5: Sheet("03_language"); step++; return;
                case 6:
                    game.UI.Language.Hide(true);
                    SaveService.Data.settings.language = "ru";
                    Loc.SetLanguage("ru");
                    Check(game.UI.Menu.Visible && Loc.T("play") == "ИГРАТЬ", "language switch rebuilds the menu in Russian");
                    step++; Wait(2.5); return;
                case 7: Sheet("04_menu_ru"); step++; return;
                case 8: Loc.SetLanguage("en"); SaveService.Data.settings.language = "en"; game.OpenShop(); step++; Wait(2.5); return;
                case 9: game.PreviewPlanet(game.Config.Planet("saturn"), 0); step++; Wait(2.0); return;
                case 10: Sheet("05_shop"); step++; return;
                case 11:
                    game.BackToMenu();
                    game.OpenLeaderboard();
                    step++; Wait(1.5); return;
                case 12:
                    if (sub == 0) { sub = 1; Sheet("05b_leaderboard"); return; }
                    sub = 0;
                    game.UI.Leaderboard.Hide(true);
                    game.Play();
                    Check(game.State == GameState.Running && game.Run.hp == 3 && game.Run.level == 1 && game.Run.xp == 0, "run starts at Level 1 / XP 0 / 3 HP");
                    var a = game.Arena;
                    a.DebugSpawn("normal", new Vector2(-2.6f, 4.5f));
                    a.DebugSpawn("fast", new Vector2(2.9f, 3.6f));
                    a.DebugSpawn("armored", new Vector2(-3.0f, -3.4f));
                    a.DebugSpawn("big", new Vector2(0.2f, 6.2f));
                    a.DebugSpawnGold(new Vector2(-2.9f, 1.4f));
                    a.DebugAim = new Vector2(0.15f, 1f);
                    step++; Wait(1.0); return;
                case 13:
                    // Wait on game time, not wall time: batch frames are slow and capped at maximumDeltaTime.
                    if (run.kills == 0 && run.elapsed < 25f) { Wait(0.5); return; }
                    run.score = 1234567; run.sessionGold = 98765;   // worst-case widths for the HUD corners
                    Sheet("06_hud"); step++; return;
                case 14:
                    Check(run.kills > 0 && run.xp > 0, "destroying meteors grants score and XP");
                    Check(game.State == GameState.Running, "still running before the scripted level-up (state " + game.State + ")");
                    run.score = 2400; run.sessionGold = 260;
                    run.xp = run.XpNeeded;
                    step++; return;
                case 15:
                    if (sub == 0) { if (!game.UI.LevelUp.Visible) return; LiftFingerOver(game.UI.LevelUp.SkipButton); }
                    if (game.State != GameState.LevelUp || Time.timeScale > 0f || game.Offers == null || sub++ < 6) { Wait(0.3); return; }
                    sub = 0;
                    Check(game.Offers.Count == 3, "three upgrade cards offered");
                    CheckSkipShown();
                    Sheet("07_levelup"); step++; return;
                case 16:
                {
                    int before = run.sessionGold;
                    var offer = game.Offers.Find(o => o.Affordable(run));
                    Check(offer != null && game.BuyUpgrade(offer), "an affordable card can be bought");
                    Check(run.sessionGold == before - offer.price && run.Level(offer.upgrade.id) == 1, "purchase spends session gold and applies tier");
                    game.CloseLevelUp();
                    step++; Wait(1.5); return;
                }
                case 17:
                    Check(game.State == GameState.Running && !game.UI.LevelUp.Visible, "run resumes after buying");
                    run.xp = run.XpNeeded;
                    step++; return;
                case 18:
                    if (sub == 0) { if (!game.UI.LevelUp.Visible) return; LiftFingerOver(game.UI.LevelUp.SkipButton); }
                    if (game.State != GameState.LevelUp || game.Offers == null || sub++ < 6) { Wait(0.3); return; }
                    sub = 0;
                    CheckSkipShown();
                    Debug.Log("PD_DEBUG before skip: " + game.UI.LevelUp.DebugState + " state=" + game.State);
                    Click(game.UI.LevelUp.SkipButton);
                    Debug.Log("PD_DEBUG after skip: " + game.UI.LevelUp.DebugState + " state=" + game.State);
                    step++; Wait(1.5); return;
                case 19:
                    Debug.Log("PD_DEBUG later: " + game.UI.LevelUp.DebugState + " state=" + game.State + " level=" + game.Run.level);
                    Check(game.State == GameState.Running, "Skip (pointer click) resumes the run");
                    Check(!game.UI.LevelUp.Visible && !game.UI.LevelUp.root.gameObject.activeSelf, "Skip closes the level-up screen");
                    game.Pause();
                    step++; Wait(1.5); return;
                case 20: Sheet("08_pause"); step++; return;
                case 21:
                    game.Resume();
                    step++; Wait(0.5); return;
                case 22:
                    if (game.State != GameState.Running || Time.timeScale < 0.99f) { Wait(0.3); return; }
                    run.hp = 1;
                    game.Arena.DebugAim = null;
                    game.Arena.DebugSpawn("normal", (Vector2)Arena.PlanetCenter + new Vector2(0, 1.9f));
                    step++; Wait(1.0); return;
                case 23:
                    if (!game.UI.Revive.Visible && run.elapsed < 200f) { Wait(0.3); return; }
                    if (sub++ < 5) { Wait(0.3); return; }
                    sub = 0;
                    Check(game.State == GameState.ReviveOffer, "last life lost: revive offered once");
                    savedBank = SaveService.Data.bankGold;
                    game.DeclineRevive();
                    step++; Wait(1.0); return;
                case 24:
                {
                    if (sub++ < 8) { Wait(0.3); return; }
                    sub = 0;
                    Check(game.State == GameState.GameOver, "declining revive ends the run");
                    var last = game.LastRun;
                    Check(SaveService.Data.dailyStreak == 1, "results keep the daily state");
                    Check(SaveService.Data.bankGold == savedBank + last.sessionGold, "unspent session gold banked");
                    Check(SaveService.Data.lastSettledRunId == last.runId && !SaveService.Data.hasSnapshot, "settlement recorded once, snapshot cleared");
                    var again = EconomyService.Settle(last.runId, last.sessionGold, last.score);
                    Check(!again.applied && SaveService.Data.bankGold == savedBank + last.sessionGold, "second settlement of the same run is ignored");
                    Check(SaveService.Data.bestScore == last.score, "best score saved");
                    Sheet("09_gameover"); step = 300; return;
                }
                case 300:
                {
                    // Rewarded placements: nothing without the Rewarded state, the promised reward with it.
                    int bank = SaveService.Data.bankGold, gain = game.LastSettlement.banked;
                    Check(game.DoubleGoldAvailable, "x2 gold is offered on the results (banked " + gain + ")");
                    game.WatchDoubleGold();
                    Check(SaveService.Data.bankGold == bank && game.DoubleGoldAvailable, "a failed ad pays nothing and x2 gold stays available");
                    PlaygamaBridgeAdapter.GrantRewardedForTests = true;
                    game.WatchDoubleGold();
                    PlaygamaBridgeAdapter.GrantRewardedForTests = false;
                    Check(SaveService.Data.bankGold == bank + gain && !game.DoubleGoldAvailable, "x2 gold pays once");
                    DailyService.DayOffsetForTests = 1;
                    game.GameOverToMenu();
                    step++; Wait(1.0); return;
                }
                case 301:
                {
                    Check(game.UI.Daily.Visible, "tomorrow's daily reward appears");
                    int bank = SaveService.Data.bankGold;
                    game.WatchDailyDouble();
                    Check(DailyService.Pending && game.UI.Daily.Visible && SaveService.Data.bankGold == bank, "a failed daily ad pays nothing");
                    PlaygamaBridgeAdapter.GrantRewardedForTests = true;
                    game.WatchDailyDouble();
                    PlaygamaBridgeAdapter.GrantRewardedForTests = false;
                    Check(SaveService.Data.bankGold == bank + game.Config.daily.rewards[1] * 2 && SaveService.Data.dailyStreak == 2, "the daily ad doubles day two");
                    DailyService.DayOffsetForTests = 0;
                    SaveService.Data.dailyDay = DailyService.Today;
                    game.Play();
                    run = game.Run;
                    Check(game.State == GameState.Running && !game.FirstRun && !game.AwaitingFirstPress, "later runs start at once");
                    run.sessionGold = 0;
                    run.xp = run.XpNeeded;
                    step++; Wait(0.5); return;
                }
                case 302:
                    if (game.State != GameState.LevelUp || game.Offers == null) { Wait(0.2); return; }
                    if (sub++ < 4) { Wait(0.3); return; }
                    sub = 0;
                    Check(!game.Offers.TrueForAll(o => o.price == 0), "only a new player's first level-up is free");
                    Sheet("10_levelup_ad"); step++; return;
                case 303:
                {
                    var offer = game.Offers[0];
                    int tier = run.Level(offer.upgrade.id);
                    bool? result = null;
                    game.WatchUpgradeAd(offer, ok => result = ok);
                    Check(result == false && run.Level(offer.upgrade.id) == tier, "a failed upgrade ad gives nothing");
                    PlaygamaBridgeAdapter.GrantRewardedForTests = true;
                    game.WatchUpgradeAd(offer, ok => result = ok);
                    PlaygamaBridgeAdapter.GrantRewardedForTests = false;
                    Check(result == true && run.Level(offer.upgrade.id) == tier + 1 && run.sessionGold == 0, "the upgrade ad applies the card for free");
                    game.CloseLevelUp();
                    step++; Wait(1.0); return;
                }
                case 304:
                    if (game.State != GameState.Running || Time.timeScale < 0.99f) { Wait(0.3); return; }
                    run.hp = 1;
                    game.Arena.DebugSpawn("normal", (Vector2)Arena.PlanetCenter + new Vector2(0, 1.9f));
                    step++; Wait(1.0); return;
                case 305:
                    if (!game.UI.Revive.Visible) { Wait(0.3); return; }
                    if (sub++ < 4) { Wait(0.3); return; }
                    sub = 0;
                    game.WatchRevive();
                    Check(game.State == GameState.ReviveOffer && game.UI.Revive.Visible && !run.reviveUsed, "a failed revive ad keeps the choice on screen");
                    PlaygamaBridgeAdapter.GrantRewardedForTests = true;
                    game.WatchRevive();
                    PlaygamaBridgeAdapter.GrantRewardedForTests = false;
                    Check(run.reviveUsed && run.hp == 1 && game.State == GameState.Running, "the revive ad continues the run with one life");
                    step++; Wait(6.0); return;
                case 306:
                    if (game.State != GameState.Running || Time.timeScale < 0.5f) { Wait(0.3); return; }
                    game.Pause();
                    step++; Wait(0.8); return;
                case 307:
                    game.EndRunFromPause();
                    step++; Wait(1.0); return;
                case 308:
                    Check(game.State == GameState.GameOver, "the revived run ends normally");
                    game.GameOverToShop();
                    Check(game.State == GameState.Shop, "results go straight to the shop");
                    game.BackToMenu();
                    game.Play();
                    Check(game.State == GameState.Running, "the moved break never blocks the next run");
                    game.Pause();
                    step++; Wait(0.8); return;
                case 309:
                    game.EndRunFromPause();
                    step = 25; Wait(1.0); return;
                case 25:
                    // Regression: leaving the menu or pressing its buttons mid-entrance used to shift them a little more
                    // on every visit (Play ended up under the Planets/Ranking row on a phone).
                    if (sub < 12)
                    {
                        if (sub % 3 == 0) game.EnterMenu();
                        else if (sub % 3 == 1) { Press(MenuColumn().Find("Play")); Press(MenuColumn().Find("Settings")); }
                        else game.OpenShop();
                        sub++; Wait(0.12); return;
                    }
                    sub = 0;
                    game.EnterMenu(); step++; Wait(2.5); return;
                case 26:
                {
                    int moved = 0;
                    foreach (RectTransform c in MenuColumn())
                        if (c.gameObject.activeSelf && menuHomes.TryGetValue(c.name, out var home) && (c.anchoredPosition - home).sqrMagnitude > 0.01f)
                        { moved++; Debug.Log($"PD_MENU_MOVED {c.name} {home} -> {c.anchoredPosition}"); }
                    Check(moved == 0, "menu buttons return to their layout after interrupted entrances");
                    step++; return;
                }
                default:
                    Debug.Log("PD_SMOKE_OK");
                    Finish(0);
                    return;
            }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            if (game != null && game.Juice != null) game.Juice.Camera.targetTexture = null;
            PlaygamaBridgeAdapter.SkipAdsForTests = false;
            PlayerPrefs.DeleteKey("bridge_storage_data_" + SaveService.Key);
            PlayerPrefs.DeleteKey(SaveService.Key);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
