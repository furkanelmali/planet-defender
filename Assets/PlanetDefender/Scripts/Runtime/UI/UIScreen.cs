using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetDefender
{
    // A full-screen layer with fade in/out. Screens are plain objects owned by UIController; all motion is unscaled.
    public abstract class UIScreen
    {
        public readonly RectTransform root;
        protected readonly CanvasGroup group;
        protected readonly GameManager game;
        protected readonly RectTransform column;
        public bool Visible { get; private set; }
        public string DebugState => $"visible={Visible} active={root.gameObject.activeSelf} alpha={group.alpha:0.00} tweening={DOTween.IsTweening(group)} shows={shows} hides={hides}";
        int shows, hides;

        protected UIScreen(Transform parent, string name, GameManager game, bool dim = false, float dimAlpha = 0.9f)
        {
            this.game = game;
            root = UIKit.Rect(parent, name).Stretch();
            group = root.gameObject.AddComponent<CanvasGroup>();
            if (dim)
            {
                var shade = UIKit.Image(root, "Dim", null, new Color(0.016f, 0.024f, 0.063f, dimAlpha));
                shade.rectTransform.Stretch();
                shade.raycastTarget = true;
            }
            column = UIKit.Rect(root, "Column");
            column.anchorMin = new Vector2(0.5f, 0); column.anchorMax = new Vector2(0.5f, 1); column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(390, 0);
            root.gameObject.SetActive(false);
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            Visible = true;
            shows++;
            group.DOKill();
            group.alpha = 0;
            group.interactable = true; group.blocksRaycasts = true;
            group.DOFade(1, 0.22f).SetUpdate(true);
            OnShow();
        }

        public void Hide(bool instant = false)
        {
            if (!Visible && !root.gameObject.activeSelf) return;
            bool wasVisible = Visible;
            Visible = false;
            hides++;
            group.DOKill();
            group.interactable = false; group.blocksRaycasts = false;
            if (wasVisible) OnHide();
            if (instant) { group.alpha = 0; root.gameObject.SetActive(false); return; }
            group.DOFade(0, 0.16f).SetUpdate(true).OnComplete(() => root.gameObject.SetActive(false));
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        public virtual void Tick() { }

        // Centred solid card for modal screens, so nothing underneath ever reads through the controls.
        protected static RectTransform Modal(RectTransform parent, float height)
        {
            var card = UIKit.Panel(parent, "Card", Palette.Deep, Palette.Line).rectTransform;
            card.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, height));
            return card;
        }

        // Entrance used across screens: rise + overshoot, staggered by delay.
        // The sequence is keyed on the element's CanvasGroup, so button juice, idle loops or OnDisable kills on the
        // transform can never cut it short, and the target is the element's remembered home position, never the
        // position it happens to have now (an interrupted entrance used to become the next one's target: the menu drifted).
        protected static void In(RectTransform rt, float delay, float rise = 40f, float fromScale = 0.92f)
        {
            if (rt == null) return;
            if (!rt.TryGetComponent<CanvasGroup>(out var cg)) cg = rt.gameObject.AddComponent<CanvasGroup>();
            DOTween.Kill(cg, true);
            if (!rt.TryGetComponent<EntranceHome>(out var home)) { home = rt.gameObject.AddComponent<EntranceHome>(); home.position = rt.anchoredPosition; }
            var target = home.position;
            rt.anchoredPosition = target - new Vector2(0, rise);
            rt.localScale = Vector3.one * fromScale;
            cg.alpha = 0;
            DOTween.Sequence().SetUpdate(true).SetTarget(cg).SetDelay(delay)
                .Append(rt.DOAnchorPos(target, 0.42f).SetEase(Ease.OutBack, 1.4f))
                .Join(rt.DOScale(1f, 0.42f).SetEase(Ease.OutBack, 1.6f))
                .Join(cg.DOFade(1f, 0.2f));
        }
    }

    public sealed class LoadingScreen : UIScreen
    {
        readonly TextMeshProUGUI dots;
        public LoadingScreen(Transform parent, GameManager game) : base(parent, "Loading", game)
        {
            var bg = UIKit.Image(root, "Bg", null, Palette.Void); bg.rectTransform.Stretch(); bg.transform.SetAsFirstSibling();
            var a = UIKit.Text(column, "TitleA", "PLANET", 40, Palette.Text, TextAlignmentOptions.Center, true, 6);
            a.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(390, 50));
            var b = UIKit.Text(column, "TitleB", "DEFENDER", 40, Palette.Plasma, TextAlignmentOptions.Center, true, 6);
            b.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(390, 50));
            dots = UIKit.Text(column, "Status", Loc.T("loading"), 13, Palette.TextDim, TextAlignmentOptions.Center, true, 4);
            dots.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(390, 24));
            dots.Fade(0.35f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
    }

    public sealed class CountdownOverlay : UIScreen
    {
        readonly TextMeshProUGUI number;
        public CountdownOverlay(Transform parent, GameManager game) : base(parent, "Countdown", game)
        {
            number = UIKit.Text(column, "Number", "3", 120, Palette.Plasma, TextAlignmentOptions.Center, true, 0);
            number.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(390, 160));
            group.blocksRaycasts = false;
        }

        public Sequence Run(System.Action done)
        {
            Show();
            group.blocksRaycasts = false;
            var seq = DOTween.Sequence().SetUpdate(true);
            for (int i = 3; i >= 0; i--)
            {
                int n = i;
                seq.AppendCallback(() =>
                {
                    number.text = n > 0 ? n.ToString() : Loc.T("countdown_go");
                    number.color = n > 0 ? Palette.Plasma : Palette.Buy;
                    number.fontSize = n > 0 ? 120 : 72;
                    number.rectTransform.localScale = Vector3.one * 1.8f;
                    number.alpha = 0;
                    AudioService.Instance?.Play(n > 0 ? Sfx.Countdown : Sfx.Revive, 0.8f, n > 0 ? 1f : 1.2f);
                });
                seq.Append(number.rectTransform.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
                seq.Join(number.Fade(1f, 0.15f));
                seq.AppendInterval(n > 0 ? 0.35f : 0.2f);
                seq.Append(number.Fade(0f, 0.12f));
            }
            seq.OnComplete(() => { Hide(true); done?.Invoke(); });
            return seq;
        }
    }

    // Music / sound / language. Shown from the main menu and from the pause screen.
    public sealed class SettingsScreen : UIScreen
    {
        readonly UIKit.ButtonParts music, sound, language;
        readonly Image languageFlag;
        readonly RectTransform card;
        System.Action onClose;

        public SettingsScreen(Transform parent, GameManager game) : base(parent, "Settings", game, true, 0.94f)
        {
            card = Modal(column, 410);
            var title = UIKit.Text(card, "Title", Loc.T("settings"), 24, Palette.Text, TextAlignmentOptions.Center, true, 4);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(320, 36));
            music = Row("Music", -86, () => { var a = AudioService.Instance; a.MusicMuted = !a.MusicMuted; Refresh(); });
            sound = Row("Sound", -154, () => { var a = AudioService.Instance; a.SfxMuted = !a.SfxMuted; Refresh(); });
            language = Row("Language", -222, game.OpenLanguage);
            languageFlag = UIKit.Image(language.face.transform, "Flag", UIKit.Assets.flags[0], Color.white);
            languageFlag.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(36, 24));
            language.label.rectTransform.Stretch(60, 16, 0, 0);
            var back = UIKit.Button(card, "Back", Loc.T("back"), ButtonStyle.Ghost, Palette.TextDim, new Vector2(302, 52), () => { Hide(); onClose?.Invoke(); });
            back.root.Place(new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(302, 52));
        }

        UIKit.ButtonParts Row(string name, float y, UnityEngine.Events.UnityAction toggle)
        {
            var b = UIKit.Button(card, name, "", ButtonStyle.Outline, Palette.Plasma, new Vector2(302, 56), toggle, 15);
            b.root.Place(new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(302, 56));
            return b;
        }

        public void Open(System.Action closed) { onClose = closed; Show(); }
        protected override void OnShow() { Refresh(); In(card, 0f, 40, 0.9f); }

        void Refresh()
        {
            var a = AudioService.Instance;
            music.SetLabel($"{Loc.T("music")}   {(a.MusicMuted ? Loc.T("off") : Loc.T("on"))}");
            sound.SetLabel($"{Loc.T("sfx")}   {(a.SfxMuted ? Loc.T("off") : Loc.T("on"))}");
            language.SetLabel($"{Loc.T("language")}   {Loc.NativeNames[Mathf.Max(0, Loc.LanguageIndex)].ToUpperInvariant()}");
            languageFlag.sprite = UIKit.Assets.flags[Mathf.Max(0, Loc.LanguageIndex)];
        }
    }

    // Language picker (flag button on the main menu, or Settings → Language). English is the default.
    public sealed class LanguageScreen : UIScreen
    {
        readonly RectTransform card;

        public LanguageScreen(Transform parent, GameManager game) : base(parent, "Language", game, true, 0.94f)
        {
            card = Modal(column, 30 + 52 + Loc.Languages.Length * 68 + 90);
            var title = UIKit.Text(card, "Title", Loc.T("language"), 24, Palette.Text, TextAlignmentOptions.Center, true, 4);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(320, 36));
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                bool current = code == Loc.Language;
                var b = UIKit.Button(card, code, Loc.NativeNames[i].ToUpperInvariant(), current ? ButtonStyle.Primary : ButtonStyle.Outline,
                    Palette.Plasma, new Vector2(302, 58), () => Pick(code), 17);
                b.root.Place(new Vector2(0.5f, 1), new Vector2(0, -82 - i * 68), new Vector2(302, 58));
                b.label.rectTransform.Stretch(76, 16, 0, 0);
                b.label.alignment = TextAlignmentOptions.Left;
                var flag = UIKit.Image(b.face.transform, "Flag", UIKit.Assets.flags[i], Color.white);
                flag.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(45, 30));
            }
            var back = UIKit.Button(card, "Back", Loc.T("back"), ButtonStyle.Ghost, Palette.TextDim, new Vector2(302, 52), () => Hide());
            back.root.Place(new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(302, 52));
        }

        protected override void OnShow() => In(card, 0f, 40, 0.9f);

        void Pick(string code)
        {
            Hide(true);
            if (code == Loc.Language) return;
            SaveService.Data.settings.language = code;
            SaveService.Save();
            Loc.SetLanguage(code);   // UIController rebuilds every screen in the new language
        }
    }
}
