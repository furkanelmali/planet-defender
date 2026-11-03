using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlanetDefender
{
    // Owns the canvas (screen-space camera, so bloom and the world share one pass) and every screen.
    public sealed class UIController : MonoBehaviour
    {
        GameManager game;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform safe, flyLayer;
        Camera cam;
        public LoadingScreen Loading { get; private set; }
        public MenuScreen Menu { get; private set; }
        public ShopScreen Shop { get; private set; }
        public HudScreen Hud { get; private set; }
        public LevelUpScreen LevelUp { get; private set; }
        public PauseScreen Pause { get; private set; }
        public ReviveScreen Revive { get; private set; }
        public GameOverScreen GameOver { get; private set; }
        public LeaderboardScreen Leaderboard { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public CountdownOverlay Countdown { get; private set; }
        public LanguageScreen Language { get; private set; }
        UIScreen[] all;
        Rect lastSafe;
        Vector2 lastScreen;

        public void Init(GameManager game, Camera camera)
        {
            this.game = game;
            cam = camera;
            UIKit.Assets = game.Assets;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5;
            canvas.sortingOrder = 100;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            // Expand: the canvas is always at least 390 × 844 units, so the design-board layout fits every aspect
            // ratio (tall phones, 16:9 phones, tablets, landscape desktop) without stacks running into each other.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.layer = 5;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform.parent, false);
            }
            Build();
        }

        void Build()
        {
            if (safe != null) Destroy(safe.gameObject);
            safe = UIKit.Rect(transform, "SafeArea").Stretch();
            Loading = new LoadingScreen(safe, game);
            Menu = new MenuScreen(safe, game);
            Shop = new ShopScreen(safe, game);
            Hud = new HudScreen(safe, game);
            LevelUp = new LevelUpScreen(safe, game);
            Pause = new PauseScreen(safe, game);
            Revive = new ReviveScreen(safe, game);
            GameOver = new GameOverScreen(safe, game);
            Leaderboard = new LeaderboardScreen(safe, game);
            Settings = new SettingsScreen(safe, game);
            Countdown = new CountdownOverlay(safe, game);
            Language = new LanguageScreen(safe, game);
            flyLayer = UIKit.Rect(safe, "Fly").Stretch();
            all = new UIScreen[] { Loading, Menu, Shop, Hud, LevelUp, Pause, Revive, GameOver, Leaderboard, Settings, Countdown, Language };
            lastSafe = default;
            Layout();
        }

        // Language switch: rebuild every screen with the new strings and restore what was visible.
        public void Rebuild()
        {
            var visible = new bool[all.Length];
            for (int i = 0; i < all.Length; i++) visible[i] = all[i].Visible;
            Build();
            for (int i = 0; i < all.Length; i++) if (visible[i] && all[i] != Countdown && all[i] != LevelUp && all[i] != GameOver) all[i].Show();
        }

        public void HideAll(params UIScreen[] except) => HideAll(false, except);

        // instant: a hard cut (results replace everything), never leaving a half-faded panel underneath.
        public void HideAll(bool instant, params UIScreen[] except)
        {
            foreach (var s in all)
            {
                bool keep = false;
                foreach (var e in except) keep |= e == s;
                if (keep) continue;
                if (s.Visible || (instant && s.root.gameObject.activeSelf)) s.Hide(instant);
            }
        }

        void Update()
        {
            if (all == null) return;
            Layout();
            foreach (var s in all) if (s.Visible) s.Tick();
        }

        // Safe area follows notches and rounded corners (GDD §12).
        void Layout()
        {
            var screen = new Vector2(cam.pixelWidth, cam.pixelHeight);
            if (Screen.safeArea == lastSafe && screen == lastScreen) return;
            lastSafe = Screen.safeArea; lastScreen = screen;
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
            safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        // A coin flies from where gold landed to the HUD counter, then the counter punches (game feel for income).
        public void FlyCoin(Vector3 world, System.Action arrived)
        {
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, screen, cam, out var from);
            Vector3 targetWorld = Hud.GoldTarget.position;
            Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(cam, targetWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, targetScreen, cam, out var to);
            for (int i = 0; i < 3; i++)
            {
                var coin = UIKit.Image(flyLayer, "FlyCoin", UIKit.Assets.coin, Palette.Gold);
                var rt = coin.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(20, 20);
                rt.anchoredPosition = from;
                rt.localScale = Vector3.zero;
                Vector2 mid = Vector2.Lerp(from, to, 0.4f) + new Vector2(Random.Range(-60f, 60f), Random.Range(20f, 80f));
                bool last = i == 2;
                DOTween.Sequence().SetDelay(i * 0.06f)
                    .Append(rt.DOScale(1.2f, 0.12f).SetEase(Ease.OutBack))
                    .Append(rt.DOAnchorPos(mid, 0.22f).SetEase(Ease.OutQuad))
                    .Append(rt.DOAnchorPos(to, 0.28f).SetEase(Ease.InQuad))
                    .Join(rt.DOScale(0.7f, 0.28f))
                    .OnComplete(() => { Destroy(coin.gameObject); if (last) arrived?.Invoke(); });
            }
        }
    }
}
