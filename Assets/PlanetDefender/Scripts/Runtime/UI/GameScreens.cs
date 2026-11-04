using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetDefender
{
    // Gameplay HUD (design board "Gameplay HUD"): level + XP bar + pause, score + combo, lives + session gold.
    public sealed class HudScreen : UIScreen
    {
        readonly TextMeshProUGUI levelText, score, combo, gold;
        readonly Image xpFill, xpGlow, goldIcon, laserFill;
        readonly RectTransform xpBar, hearts, goldBox, hint, laserBox;
        readonly TextMeshProUGUI hintTitle, hintBody;
        readonly List<Image> heartImages = new List<Image>();
        readonly RectTransform pointer;
        readonly TextMeshProUGUI pointerText;
        readonly TextMeshProUGUI sectorLine, missionLine, bannerTitle, bannerBody;
        readonly RectTransform banner;
        readonly CanvasGroup bannerGroup;
        readonly Queue<(string title, string body, Color color)> banners = new Queue<(string, string, Color)>();
        bool bannerBusy;
        float nextGoalRefresh;
        int shownScore, shownGold, shownHp = -1, shownLevel;
        float shownXp;
        Tween scoreTween, goldTween, hintTween;

        public RectTransform GoldTarget => goldIcon.rectTransform;
        const float CornerWidth = 132f;

        static void LeftOfCorner(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(0, y - height); rt.offsetMax = new Vector2(-(CornerWidth + 12), y);
        }

        public HudScreen(Transform parent, GameManager game) : base(parent, "Hud", game)
        {
            group.blocksRaycasts = true;
            var top = UIKit.Rect(root, "Top");
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(-32, 120); top.anchoredPosition = new Vector2(0, -16);
            var topRow = UIKit.Rect(top, "Row");
            topRow.anchorMin = new Vector2(0, 1); topRow.anchorMax = new Vector2(1, 1); topRow.pivot = new Vector2(0.5f, 1);
            topRow.sizeDelta = new Vector2(0, 44); topRow.anchoredPosition = Vector2.zero;

            levelText = UIKit.Text(topRow, "Level", "LV 1", 13, Palette.Xp, TextAlignmentOptions.Left, true, 2);
            levelText.rectTransform.Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(54, 24));
            var pause = UIKit.IconButton(topRow, "Pause", UIKit.Assets.pause, new Vector2(44, 44), game.Pause, "Pause");
            pause.root.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(44, 44));

            xpBar = UIKit.Rect(topRow, "Xp");
            xpBar.anchorMin = new Vector2(0, 0.5f); xpBar.anchorMax = new Vector2(1, 0.5f); xpBar.pivot = new Vector2(0.5f, 0.5f);
            xpBar.offsetMin = new Vector2(62, -6); xpBar.offsetMax = new Vector2(-56, 6);
            var track = UIKit.Image(xpBar, "Track", UIKit.Assets.pill, Palette.Panel, true); track.rectTransform.Stretch();
            var border = UIKit.Image(xpBar, "Border", UIKit.Assets.pillOutline, Palette.Line, true); border.rectTransform.Stretch();
            xpGlow = UIKit.Image(xpBar, "Glow", UIKit.Assets.pill, Palette.Xp.WithAlpha(0.35f), true);
            xpGlow.rectTransform.anchorMin = Vector2.zero; xpGlow.rectTransform.anchorMax = new Vector2(0, 1); xpGlow.rectTransform.pivot = new Vector2(0, 0.5f);
            xpGlow.rectTransform.offsetMin = new Vector2(-3, -4); xpGlow.rectTransform.offsetMax = new Vector2(3, 4);
            xpFill = UIKit.Image(xpBar, "Fill", UIKit.Assets.pill, Palette.Xp.Hdr(1.25f), true);
            xpFill.rectTransform.anchorMin = Vector2.zero; xpFill.rectTransform.anchorMax = new Vector2(0, 1); xpFill.rectTransform.pivot = new Vector2(0, 0.5f);
            xpFill.rectTransform.offsetMin = new Vector2(1, 1); xpFill.rectTransform.offsetMax = new Vector2(1, -1);

            // Left corner: score + combo take whatever width the right corner (lives, gold) leaves, never overlapping it.
            score = UIKit.Text(top, "Score", "0", 30, Palette.Text, TextAlignmentOptions.Left, true, 1);
            LeftOfCorner(score.rectTransform, -54, 36);
            score.enableAutoSizing = true; score.fontSizeMin = 18; score.fontSizeMax = 30; score.textWrappingMode = TextWrappingModes.NoWrap;
            combo = UIKit.Text(top, "Combo", "", 14, Palette.Plasma, TextAlignmentOptions.Left, true, 1);
            LeftOfCorner(combo.rectTransform, -90, 20);
            combo.textWrappingMode = TextWrappingModes.NoWrap;
            sectorLine = UIKit.Text(top, "Sector", "", 11, Palette.PlasmaSoft, TextAlignmentOptions.Left, true, 1);
            LeftOfCorner(sectorLine.rectTransform, -112, 16);
            sectorLine.enableAutoSizing = true; sectorLine.fontSizeMin = 8; sectorLine.fontSizeMax = 11; sectorLine.textWrappingMode = TextWrappingModes.NoWrap;
            missionLine = UIKit.Text(top, "Mission", "", 11, Palette.TextDim, TextAlignmentOptions.Left, false, 0);
            LeftOfCorner(missionLine.rectTransform, -130, 16);
            missionLine.enableAutoSizing = true; missionLine.fontSizeMin = 8; missionLine.fontSizeMax = 11; missionLine.textWrappingMode = TextWrappingModes.NoWrap;

            hearts = UIKit.Rect(top, "Hearts");
            hearts.Place(new Vector2(1, 1), new Vector2(0, -56), new Vector2(CornerWidth, 24));
            var hl = hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4; hl.childAlignment = TextAnchor.MiddleRight; hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            goldBox = UIKit.Rect(top, "Gold");
            goldBox.Place(new Vector2(1, 1), new Vector2(0, -86), new Vector2(CornerWidth, 24));
            gold = UIKit.Text(goldBox, "Value", "0", 16, Palette.Gold, TextAlignmentOptions.Right, true);
            gold.rectTransform.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 24));
            goldIcon = UIKit.Image(goldBox, "Coin", UIKit.Assets.coin, Palette.Gold);
            goldIcon.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-gold.preferredWidth - 8, 0), new Vector2(16, 16));

            laserBox = UIKit.Rect(top, "Laser");
            laserBox.Place(new Vector2(1, 1), new Vector2(0, -114), new Vector2(84, 6));
            var laserTrack = UIKit.Image(laserBox, "Track", UIKit.Assets.pill, Palette.Panel, true); laserTrack.rectTransform.Stretch();
            laserFill = UIKit.Image(laserBox, "Fill", UIKit.Assets.pill, Palette.Xp, true);
            laserFill.rectTransform.anchorMin = Vector2.zero; laserFill.rectTransform.anchorMax = new Vector2(0, 1); laserFill.rectTransform.pivot = new Vector2(0, 0.5f);
            laserFill.rectTransform.offsetMin = Vector2.zero; laserFill.rectTransform.offsetMax = Vector2.zero;

            hint = UIKit.Panel(column, "Hint", Palette.Panel.WithAlpha(0.88f), Palette.Line).rectTransform;
            hint.Place(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(342, 72));
            hintTitle = UIKit.Text(hint, "Title", "", 13, Palette.Plasma, TextAlignmentOptions.Center, true, 2);
            hintTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(320, 20));
            hintBody = UIKit.Text(hint, "Body", "", 14, Palette.TextDim, TextAlignmentOptions.Center, false);
            hintBody.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(320, 36));
            hint.gameObject.SetActive(false);

            pointer = UIKit.Rect(root, "GoldPointer");
            pointer.sizeDelta = new Vector2(200, 30);
            pointerText = UIKit.Text(pointer, "Text", Loc.T("hint_gold"), 13, Palette.Gold, TextAlignmentOptions.Center, true, 2);
            pointerText.rectTransform.Stretch();
            pointerText.fontSharedMaterial = UIKit.OutlinedFont(UIKit.Assets.font);
            pointer.gameObject.SetActive(false);

            banner = UIKit.Rect(column, "Banner");
            banner.Place(new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(370, 58));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
            bannerTitle = UIKit.Text(banner, "Title", "", 22, Palette.Plasma, TextAlignmentOptions.Center, true, 3);
            bannerTitle.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(370, 30));
            bannerTitle.fontSharedMaterial = UIKit.OutlinedFont(UIKit.Assets.font);
            bannerTitle.enableAutoSizing = true; bannerTitle.fontSizeMin = 14; bannerTitle.fontSizeMax = 22; bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;
            bannerBody = UIKit.Text(banner, "Body", "", 14, Palette.Gold, TextAlignmentOptions.Center, true, 1);
            bannerBody.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(370, 22));
            bannerBody.fontSharedMaterial = UIKit.OutlinedFont(UIKit.Assets.font);
            bannerBody.enableAutoSizing = true; bannerBody.fontSizeMin = 10; bannerBody.fontSizeMax = 14; bannerBody.textWrappingMode = TextWrappingModes.NoWrap;
            banner.gameObject.SetActive(false);
        }

        // Short goal announcements, one at a time, so a sector and a mission landing together both get read.
        public void Announce(string title, string body, Color color)
        {
            banners.Enqueue((title, body, color));
            if (!bannerBusy) NextBanner();
        }

        void NextBanner()
        {
            if (banners.Count == 0 || !Visible) { bannerBusy = false; banner.gameObject.SetActive(false); return; }
            bannerBusy = true;
            var item = banners.Dequeue();
            bannerTitle.text = item.title; bannerTitle.color = item.color;
            bannerBody.text = item.body;
            banner.gameObject.SetActive(true);
            bannerGroup.alpha = 0;
            banner.localScale = Vector3.one * 0.8f;
            DOTween.Sequence().SetUpdate(true).SetTarget(bannerGroup)
                .Append(bannerGroup.DOFade(1f, 0.15f))
                .Join(banner.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f))
                .AppendInterval(banners.Count > 0 ? 1.1f : 1.6f)
                .Append(bannerGroup.DOFade(0f, 0.2f))
                .OnComplete(NextBanner);
        }

        void RefreshGoalLines(RunState run)
        {
            if (Time.unscaledTime < nextGoalRefresh) return;
            nextGoalRefresh = Time.unscaledTime + 0.25f;
            int left = Mathf.CeilToInt(game.Config.SectorTime(run.sectorsPassed) - run.elapsed);
            sectorLine.text = Loc.F("sector_line", run.sectorsPassed + 1, UIKit.Clock(left), UIKit.Num(game.Config.SectorBonus(run.sectorsPassed)));
            var mission = MissionService.Closest();
            missionLine.text = mission != null ? $"{mission.Title}  <color={Palette.Tag(Palette.Text)}>{mission.Progress}</color>" : "";
        }

        protected override void OnShow()
        {
            var run = game.Run;
            shownScore = run.score; shownGold = run.sessionGold; shownLevel = run.level; shownXp = (float)run.xp / run.XpNeeded;
            score.text = UIKit.Num(shownScore);
            gold.text = UIKit.Num(shownGold);
            levelText.text = Loc.F("lv", run.level);
            combo.text = "";
            BuildHearts(run.maxHp);
            shownHp = -1;
            SetXp(shownXp);
            LayoutGold();
            nextGoalRefresh = 0;
            RefreshGoalLines(run);
        }

        void BuildHearts(int max)
        {
            for (int i = hearts.childCount - 1; i >= 0; i--) Object.Destroy(hearts.GetChild(i).gameObject);
            heartImages.Clear();
            for (int i = 0; i < max; i++)
            {
                var h = UIKit.Image(hearts, "Heart", UIKit.Assets.heart, Palette.Danger);
                h.rectTransform.sizeDelta = new Vector2(20, 20);
                heartImages.Add(h);
            }
        }

        public override void Tick()
        {
            var run = game.Run;
            if (run == null) return;
            if (run.score != shownScore)
            {
                int from = shownScore; shownScore = run.score;
                scoreTween?.Kill();
                scoreTween = UIKit.Count(score, from, shownScore, 0.35f);
                score.rectTransform.DOKill(true);
                score.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.2f, 5, 0.5f).SetUpdate(true);
            }
            if (run.sessionGold != shownGold) SetGold(run.sessionGold);
            if (run.hp != shownHp) SetHp(run.hp, shownHp >= 0);
            if (heartImages.Count != run.maxHp) { BuildHearts(run.maxHp); shownHp = -1; }
            float xp = Mathf.Clamp01((float)run.xp / run.XpNeeded);
            if (run.level != shownLevel) { shownLevel = run.level; shownXp = 0; levelText.text = Loc.F("lv", run.level); levelText.rectTransform.DOPunchScale(Vector3.one * 0.4f, 0.4f, 6, 0.6f).SetUpdate(true); }
            shownXp = Mathf.MoveTowards(shownXp, xp, Time.unscaledDeltaTime * 1.6f);
            SetXp(shownXp);
            xpGlow.color = Palette.Xp.WithAlpha(xp > 0.85f ? 0.35f + Mathf.PingPong(Time.unscaledTime * 2f, 0.4f) : 0.25f);
            combo.text = run.Combo > 1.01f ? Loc.F("combo", run.Combo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)) : "";
            float laser = game.Arena.LaserReady;
            laserBox.gameObject.SetActive(laser >= 0);
            if (laser >= 0)
            {
                laserFill.rectTransform.anchorMax = new Vector2(laser, 1);
                laserFill.color = laser >= 1 ? Palette.Xp.Hdr(1.4f + Mathf.PingPong(Time.unscaledTime * 3f, 0.8f)) : Palette.Xp.WithAlpha(0.7f);
            }
            RefreshGoalLines(run);
            TrackGoldPointer();
        }

        void SetXp(float fraction)
        {
            xpFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, fraction), 1);
            xpGlow.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, fraction), 1);
        }

        public void SetGold(int value)
        {
            int from = shownGold; shownGold = value;
            goldTween?.Kill();
            goldTween = UIKit.Count(gold, from, value, 0.4f).OnUpdate(LayoutGold);
        }

        void LayoutGold() => goldIcon.rectTransform.anchoredPosition = new Vector2(-gold.preferredWidth - 8, 0);

        public void PunchGold()
        {
            goldBox.DOKill(true);
            goldBox.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.6f).SetUpdate(true);
        }

        void SetHp(int hp, bool animate)
        {
            for (int i = 0; i < heartImages.Count; i++)
            {
                bool full = i < hp;
                var img = heartImages[i];
                bool was = img.sprite == UIKit.Assets.heart;
                img.sprite = full ? UIKit.Assets.heart : UIKit.Assets.heartEmpty;
                img.color = full ? Palette.Danger : Palette.Line;
                if (animate && was != full)
                {
                    img.rectTransform.DOKill(true);
                    img.rectTransform.DOPunchScale(Vector3.one * (full ? 0.5f : 0.7f), 0.45f, 7, 0.6f).SetUpdate(true);
                    if (!full) img.rectTransform.DOShakeAnchorPos(0.35f, 6, 20).SetUpdate(true);
                }
            }
            shownHp = hp;
        }

        public void ComboPop(float value)
        {
            combo.rectTransform.DOKill(true);
            combo.rectTransform.localScale = Vector3.one * 1.5f;
            combo.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void ShowHint(string title, string body, float autoHide = 0)
        {
            hintTween?.Kill();
            hint.gameObject.SetActive(true);
            hintTitle.text = title; hintBody.text = body;
            In(hint, 0, 30);
            if (autoHide > 0) hintTween = DOVirtual.DelayedCall(autoHide, HideHint, true);
        }

        public void HideHint()
        {
            if (!hint.gameObject.activeSelf) return;
            hintTween?.Kill();
            var cg = hint.GetComponent<CanvasGroup>();
            DOTween.Kill(cg, true);
            hint.DOKill(true);
            hintTween = DOTween.Sequence().SetUpdate(true)
                .Append(hint.DOAnchorPosY(hint.anchoredPosition.y - 20, 0.2f))
                .Join(cg.DOFade(0, 0.2f))
                .OnComplete(() => { hint.gameObject.SetActive(false); hint.anchoredPosition = new Vector2(0, 40); });
        }

        System.Func<Vector2?> pointed;
        public void PointAtGold(GoldPickup g) =>
            PointAt(() => g != null && g.alive ? g.position + Vector2.up * (g.radius - 0.4f) : (Vector2?)null, Loc.T("hint_gold"), Palette.Gold);

        public void ClearPointer() { pointed = null; pointer.gameObject.SetActive(false); }

        // A label that follows something in the world until it is gone.
        public void PointAt(System.Func<Vector2?> target, string text, Color color)
        {
            pointed = target;
            pointer.gameObject.SetActive(true);
            pointerText.text = text;
            pointerText.color = color;
            pointer.localScale = Vector3.zero;
            pointer.DOKill();
            pointer.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        void TrackGoldPointer()
        {
            if (!pointer.gameObject.activeSelf) return;
            var at = pointed?.Invoke();
            if (at == null) { ClearPointer(); return; }
            var cam = game.Juice.Camera;
            Vector2 screen = cam.WorldToScreenPoint((Vector3)at.Value + Vector3.up * 0.95f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root.parent, screen, cam, out var local);
            // Keep the label on screen even when the gold enters from a side edge.
            var area = ((RectTransform)root.parent).rect;
            float half = pointerText.preferredWidth * 0.5f + 12f;
            local.x = Mathf.Clamp(local.x, area.xMin + half, area.xMax - half);
            local.y = Mathf.Clamp(local.y, area.yMin + 40f, area.yMax - 160f);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.anchoredPosition = local;
        }

        protected override void OnHide()
        {
            pointer.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
            banners.Clear();
            DOTween.Kill(bannerGroup);
            banner.DOKill();
            bannerBusy = false;
            banner.gameObject.SetActive(false);
        }
    }

    // Level-up (design board "Level-up"): gameplay is frozen; three cards, at most one purchase, Skip always works.
    public sealed class LevelUpScreen : UIScreen
    {
        readonly TextMeshProUGUI level, gold;
        readonly RectTransform header, cards;
        readonly UIKit.ButtonParts skip;
        public UnityEngine.UI.Button SkipButton => skip.button;
        readonly List<(RectTransform card, UIKit.ButtonParts buy, UpgradeOffer offer, Image border)> rows = new List<(RectTransform, UIKit.ButtonParts, UpgradeOffer, Image)>();
        bool locked, adOffered;

        public LevelUpScreen(Transform parent, GameManager game) : base(parent, "LevelUp", game, true, 0.92f)
        {
            header = UIKit.Rect(column, "Header");
            header.Place(new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(390, 120));
            var label = UIKit.Text(header, "Label", Loc.T("level_up"), 13, Palette.Xp, TextAlignmentOptions.Center, true, 4);
            label.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(390, 20));
            level = UIKit.Text(header, "Level", "2", 52, Palette.Text, TextAlignmentOptions.Center, true);
            level.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(390, 60));
            var goldRow = UIKit.Rect(header, "Gold");
            goldRow.Place(new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(240, 24));
            var coin = UIKit.Image(goldRow, "Coin", UIKit.Assets.coin, Palette.Gold);
            coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(16, 16));
            gold = UIKit.Text(goldRow, "Value", "0", 18, Palette.Gold, TextAlignmentOptions.Left, true);
            gold.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(200, 24));

            cards = UIKit.Rect(column, "Cards");
            cards.anchorMin = new Vector2(0.5f, 1); cards.anchorMax = new Vector2(0.5f, 1); cards.pivot = new Vector2(0.5f, 1);
            cards.sizeDelta = new Vector2(350, 330); cards.anchoredPosition = new Vector2(0, -200);
            UIKit.VStack(cards, 12);

            skip = UIKit.Button(column, "Skip", Loc.T("skip"), ButtonStyle.Ghost, Palette.TextDim, new Vector2(350, 52), () => { if (!locked) game.SkipUpgrade(); });
            skip.root.Place(new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(350, 52));
            skip.face.sprite = UIKit.Assets.pillOutline;
        }

        public void Open(RunState run, List<UpgradeOffer> offers)
        {
            locked = false;
            adOffered = false;
            Show();
            level.text = run.level.ToString();
            bool gift = offers.Count > 0 && offers.TrueForAll(o => o.price == 0);
            gold.text = gift ? $"<size=13><color={Palette.Tag(Palette.Buy)}>{Loc.T("first_free")}</color></size>"
                : UIKit.Num(run.sessionGold) + "  <size=13><color=#93A3CF>" + Loc.T("session_gold") + "</color></size>";
            gold.transform.parent.GetChild(0).gameObject.SetActive(!gift);
            gold.alignment = gift ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            gold.textWrappingMode = TextWrappingModes.NoWrap;
            gold.rectTransform.Place(gift ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f), gift ? Vector2.zero : new Vector2(40, 0), new Vector2(gift ? 340 : 200, 24));
            for (int i = cards.childCount - 1; i >= 0; i--) Object.Destroy(cards.GetChild(i).gameObject);
            rows.Clear();
            for (int i = 0; i < offers.Count; i++) Card(run, offers[i], i);
            header.DOKill(true);
            level.rectTransform.localScale = Vector3.one * 2.2f;
            level.rectTransform.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
            In(skip.root, 0.35f, 20);
        }

        void Card(RunState run, UpgradeOffer offer, int i)
        {
            bool affordable = offer.Affordable(run);
            var card = UIKit.Panel(cards, "Card", affordable ? Palette.Panel : Palette.Deep);
            card.Size(96);
            var border = UIKit.Image(card.transform, "Border", UIKit.Assets.panelOutline, affordable && i == 0 ? Palette.Plasma : Palette.Line, true);
            border.rectTransform.Stretch();
            var iconBg = UIKit.Image(card.transform, "IconBg", UIKit.Assets.panel, Palette.Deep, true);
            iconBg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(60, 60));
            var icon = UIKit.Image(iconBg.transform, "Icon", UIKit.Assets.upgradeIcons[offer.index], IconColor(offer.index));
            icon.rectTransform.Stretch(12, 12, 12, 12);
            var name = UIKit.Text(card.transform, "Name", Loc.T("up_" + offer.upgrade.id), 15, Palette.Text, TextAlignmentOptions.Left, true, 1);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(88, -16), new Vector2(160, 22));
            name.enableAutoSizing = true; name.fontSizeMin = 11; name.fontSizeMax = 15; name.textWrappingMode = TextWrappingModes.NoWrap;
            string tier = offer.currentLevel == 0 ? Loc.T("new") : Loc.F("tier", offer.currentLevel, offer.currentLevel + 1);
            var desc = UIKit.Text(card.transform, "Desc", $"{Loc.T("up_" + offer.upgrade.id + "_d")}  <color={Palette.Tag(Palette.Plasma)}>{tier}</color>", 12, Palette.TextDim, TextAlignmentOptions.TopLeft, false);
            desc.rectTransform.Place(new Vector2(0, 1), new Vector2(88, -40), new Vector2(160, 46));
            // One out-of-reach card per level-up can be taken for an ad: the honest alternative to staring at a greyed-out price.
            bool viaAd = !affordable && !adOffered && game.RewardedAvailable;
            if (viaAd) adOffered = true;
            string priceLabel = offer.price == 0 ? Loc.T("free") : viaAd ? Loc.T("ad_free") : UIKit.Num(offer.price);
            var buy = UIKit.Button(card.transform, "Buy", priceLabel, ButtonStyle.Buy, Palette.Buy, new Vector2(82, 44), null, 14);
            buy.root.Place(new Vector2(1, 0.5f), new Vector2(-14, 2), new Vector2(82, 44));
            buy.SetCoin(offer.price > 0 && !viaAd);
            buy.SetInteractable(affordable || viaAd);
            var rt = card.rectTransform;
            if (viaAd) buy.button.onClick.AddListener(() => OnBuyWithAd(offer, rt, buy));
            else buy.button.onClick.AddListener(() => OnBuy(offer, rt, buy));
            rows.Add((rt, buy, offer, border));
            // Cards slide up one after another; layout owns the position, so animate a child-safe offset via scale + fade.
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            rt.localScale = Vector3.one * 0.8f;
            DOTween.Sequence().SetUpdate(true).SetDelay(0.12f + i * 0.08f)
                .Append(cg.DOFade(1, 0.18f))
                .Join(rt.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 1.8f))
                .OnComplete(() => { if (affordable) buy.root.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true); });
        }

        static Color IconColor(int index) =>
            index == 4 ? Palette.Meteor : index == 5 ? Palette.PlasmaSoft : index == 6 || index == 7 ? Palette.Xp : Palette.Plasma;

        void OnBuy(UpgradeOffer offer, RectTransform card, UIKit.ButtonParts buy)
        {
            if (locked) return;
            if (!game.BuyUpgrade(offer)) { buy.juice.Deny(); AudioService.Instance?.Play(Sfx.Denied); return; }
            Taken(card);
        }

        void OnBuyWithAd(UpgradeOffer offer, RectTransform card, UIKit.ButtonParts buy)
        {
            if (locked) return;
            locked = true;
            buy.SetInteractable(false);
            skip.SetInteractable(false);
            game.WatchUpgradeAd(offer, granted =>
            {
                skip.SetInteractable(true);
                if (granted) { Taken(card); return; }
                locked = false;
                buy.SetLabel(Loc.T("ad_retry"));
                buy.SetInteractable(true);
                AudioService.Instance?.Play(Sfx.Denied);
            });
        }

        void Taken(RectTransform card)
        {
            locked = true;
            foreach (var row in rows)
            {
                row.buy.root.DOKill(); row.buy.root.localScale = Vector3.one;
                if (row.card == card) continue;
                row.card.GetComponent<CanvasGroup>().DOFade(0.25f, 0.2f).SetUpdate(true);
                row.buy.SetInteractable(false);
            }
            card.DOKill(true);
            card.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f).SetUpdate(true);
            gold.text = UIKit.Num(game.Run.sessionGold) + "  <size=13><color=#93A3CF>" + Loc.T("session_gold") + "</color></size>";
            DOVirtual.DelayedCall(0.45f, game.CloseLevelUp, true);
        }
    }

    public sealed class PauseScreen : UIScreen
    {
        public PauseScreen(Transform parent, GameManager game) : base(parent, "Pause", game, true, 0.88f)
        {
            var title = UIKit.Text(column, "Title", Loc.T("pause"), 30, Palette.Text, TextAlignmentOptions.Center, true, 5);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(390, 40));
            var resume = UIKit.Button(column, "Resume", Loc.T("resume"), ButtonStyle.Primary, Palette.Plasma, new Vector2(300, 62), game.Resume);
            resume.root.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(300, 62));
            var settings = UIKit.Button(column, "Settings", Loc.T("settings"), ButtonStyle.Outline, Palette.Plasma, new Vector2(300, 52), game.OpenSettings);
            settings.root.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(300, 52));
            var end = UIKit.Button(column, "EndRun", Loc.T("end_run"), ButtonStyle.Outline, Palette.Danger, new Vector2(300, 52), game.EndRunFromPause);
            end.root.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(300, 52));
            var note = UIKit.Text(column, "Note", Loc.T("settle_note"), 12, Palette.TextDim, TextAlignmentOptions.Center, false);
            note.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -136), new Vector2(300, 36));
            items = new[] { title.rectTransform, resume.root, settings.root, end.root };
        }
        readonly RectTransform[] items;
        protected override void OnShow() { for (int i = 0; i < items.Length; i++) In(items[i], i * 0.04f, 24); }
    }

    // Revive (GDD §10.2): offered once per run, only when rewarded ads are supported; End Run is always free.
    public sealed class ReviveScreen : UIScreen
    {
        readonly UIKit.ButtonParts watch, end;
        readonly RectTransform box;
        readonly TextMeshProUGUI status;
        public ReviveScreen(Transform parent, GameManager game) : base(parent, "Revive", game, true, 0.85f)
        {
            box = UIKit.Rect(column, "Box");
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(342, 330));
            var heart = UIKit.Image(box, "Heart", UIKit.Assets.heart, Palette.Danger);
            heart.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(72, 72));
            heart.rectTransform.DOScale(1.12f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            var title = UIKit.Text(box, "Title", Loc.T("revive_title"), 26, Palette.Danger, TextAlignmentOptions.Center, true, 3);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(342, 36));
            var body = UIKit.Text(box, "Body", Loc.T("revive_body"), 15, Palette.TextDim, TextAlignmentOptions.Center, false);
            body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(320, 50));
            watch = UIKit.Button(box, "Watch", Loc.T("watch_ad"), ButtonStyle.Buy, Palette.Buy, new Vector2(342, 62), game.WatchRevive, 17);
            watch.root.Place(new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(342, 62));
            end = UIKit.Button(box, "End", Loc.T("no_thanks"), ButtonStyle.Outline, Palette.TextDim, new Vector2(342, 50), game.DeclineRevive);
            end.root.Place(new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(342, 50));
            status = UIKit.Text(box, "Status", "", 12, Palette.Gold, TextAlignmentOptions.Center, false);
            status.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 136), new Vector2(342, 20));
        }
        protected override void OnShow() { status.text = ""; In(box, 0.05f, 50, 0.8f); watch.juice.transform.DOScale(1.04f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetDelay(0.5f); }
        protected override void OnHide() { watch.juice.transform.DOKill(); watch.juice.transform.localScale = Vector3.one; }
        public void SetBusy(bool busy)
        {
            watch.SetInteractable(!busy); end.SetInteractable(!busy);
            status.text = busy ? Loc.T("ad_loading") : "";
        }
        public void ShowAdFailed() { status.text = Loc.T("ad_failed"); AudioService.Instance?.Play(Sfx.Denied); }
    }

    // Game over (design board "Game over"): score count-up, new-best badge, run stats, banked gold.
    public sealed class GameOverScreen : UIScreen
    {
        readonly TextMeshProUGUI score, kills, level, earned, spent, banked, runLine;
        readonly RectTransform title, badge, stats, bankBox, buttons, goalsBox;
        readonly UIKit.ButtonParts ranking, planets, doubleGold;
        readonly GoalsView goals;
        int bankedShown;

        public GameOverScreen(Transform parent, GameManager game) : base(parent, "GameOver", game, true, 0.9f)
        {
            title = UIKit.Text(column, "Title", Loc.T("game_over"), 30, Palette.Danger, TextAlignmentOptions.Center, true, 3).rectTransform;
            title.Place(new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(390, 36));
            score = UIKit.Text(column, "Score", "0", 48, Palette.Text, TextAlignmentOptions.Center, true);
            score.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(390, 54));
            runLine = UIKit.Text(column, "RunLine", "", 13, Palette.TextDim, TextAlignmentOptions.Center, true, 1);
            runLine.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(370, 20));
            runLine.enableAutoSizing = true; runLine.fontSizeMin = 10; runLine.fontSizeMax = 13; runLine.textWrappingMode = TextWrappingModes.NoWrap;
            badge = UIKit.Rect(column, "Badge");
            badge.Place(new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(120, 24));
            var badgeBg = UIKit.Image(badge, "Bg", UIKit.Assets.pill, Palette.Gold, true); badgeBg.rectTransform.Stretch();
            var badgeText = UIKit.Text(badge, "Text", Loc.T("new_best"), 12, Palette.Void, TextAlignmentOptions.Center, true, 2); badgeText.rectTransform.Stretch();

            stats = UIKit.Panel(column, "Stats", Palette.Panel, Palette.Line).rectTransform;
            stats.Place(new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(350, 112));
            kills = Stat(stats, Loc.T("meteors_destroyed"), new Vector2(0, 1), Palette.Text);
            level = Stat(stats, Loc.T("reached_level"), new Vector2(1, 1), Palette.Xp);
            earned = Stat(stats, Loc.T("gold_earned"), new Vector2(0, 0), Palette.Gold);
            spent = Stat(stats, Loc.T("gold_spent"), new Vector2(1, 0), Palette.TextDim);

            bankBox = UIKit.Panel(column, "Bank", Palette.Deep, Palette.Gold).rectTransform;
            bankBox.Place(new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(350, 50));
            var bl = UIKit.Text(bankBox, "Label", Loc.T("banked"), 13, Palette.Text, TextAlignmentOptions.Left, true, 2);
            bl.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(160, 24));
            banked = UIKit.Text(bankBox, "Value", "+0", 24, Palette.Gold, TextAlignmentOptions.Right, true);
            banked.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(160, 30));

            doubleGold = UIKit.Button(column, "DoubleGold", "", ButtonStyle.Buy, Palette.Buy, new Vector2(350, 46), game.WatchDoubleGold, 15);
            doubleGold.root.Place(new Vector2(0.5f, 1), new Vector2(0, -318), new Vector2(350, 46));

            goalsBox = UIKit.Panel(column, "Goals", Palette.Panel, Palette.Line).rectTransform;
            goalsBox.Place(new Vector2(0.5f, 1), new Vector2(0, -372), new Vector2(350, 194));
            goals = new GoalsView(goalsBox, game, 326);

            buttons = UIKit.Rect(column, "Buttons");
            buttons.Place(new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(350, 136));
            var retry = UIKit.Button(buttons, "Retry", Loc.T("retry"), ButtonStyle.Primary, Palette.Plasma, new Vector2(350, 62), game.Retry);
            retry.root.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(350, 62));
            planets = UIKit.Button(buttons, "Planets", Loc.T("planets"), ButtonStyle.Outline, Palette.Plasma, new Vector2(169, 50), game.GameOverToShop);
            ranking = UIKit.Button(buttons, "Ranking", Loc.T("ranking"), ButtonStyle.Outline, Palette.Xp, new Vector2(169, 50), game.OpenLeaderboard);
            var menu = UIKit.IconButton(column, "Menu", UIKit.Assets.arrowLeft, new Vector2(44, 44), game.GameOverToMenu, "Menu");
            menu.root.Place(new Vector2(0, 1), new Vector2(20, -20), new Vector2(44, 44));
        }

        static TextMeshProUGUI Stat(RectTransform parent, string label, Vector2 corner, Color color)
        {
            var cell = UIKit.Rect(parent, label);
            cell.anchorMin = new Vector2(corner.x * 0.5f, corner.y * 0.5f);
            cell.anchorMax = new Vector2(corner.x * 0.5f + 0.5f, corner.y * 0.5f + 0.5f);
            cell.offsetMin = new Vector2(18, 4); cell.offsetMax = new Vector2(-8, -6);
            var l = UIKit.Text(cell, "Label", label, 12, Palette.TextDim, TextAlignmentOptions.TopLeft, false);
            l.rectTransform.Place(new Vector2(0, 1), Vector2.zero, new Vector2(150, 18));
            var v = UIKit.Text(cell, "Value", "0", 20, color, TextAlignmentOptions.TopLeft, true);
            v.rectTransform.Place(new Vector2(0, 1), new Vector2(0, -20), new Vector2(150, 28));
            return v;
        }

        public void Open(RunState run, Settlement settlement)
        {
            Show();
            const bool rank = true;
            ranking.root.gameObject.SetActive(rank);
            planets.root.Place(rank ? new Vector2(0, 0) : new Vector2(0.5f, 0), Vector2.zero, new Vector2(rank ? 169 : 350, 50));
            ranking.root.Place(new Vector2(1, 0), Vector2.zero, new Vector2(169, 50));
            score.text = "0";
            kills.text = UIKit.Num(run.kills); level.text = run.level.ToString();
            earned.text = UIKit.Num(run.goldEarned); spent.text = UIKit.Num(run.goldSpent);
            banked.text = "+0";
            bankedShown = settlement.banked;
            badge.gameObject.SetActive(false);
            var data = SaveService.Data;
            string line = Loc.F("run_line", UIKit.Clock(Mathf.FloorToInt(run.elapsed)), run.sectorsPassed + 1);
            if (!settlement.newBest && data.bestScore > run.score) line += "  ·  " + Loc.F("to_best", UIKit.Num(data.bestScore - run.score));
            runLine.text = line;
            doubleGold.root.gameObject.SetActive(game.DoubleGoldAvailable);
            doubleGold.SetLabel(Loc.F("double_gold", UIKit.Num(settlement.banked)));
            doubleGold.SetInteractable(true);
            goals.Refresh();
            bool canBuy = EconomyService.NextPlanet(game.Config) is PlanetConfig next && EconomyService.CanBuy(next);
            planets.juice.transform.DOKill();
            planets.juice.transform.localScale = Vector3.one;
            if (canBuy) planets.juice.transform.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            In(title, 0f, 30); In(stats, 0.25f); In(bankBox, 0.35f); In(goalsBox, 0.4f); In(buttons, 0.45f);
            if (game.DoubleGoldAvailable) In(doubleGold.root, 0.55f);
            title.DOKill(true);
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.AppendInterval(0.15f);
            seq.Append(UIKit.Count(score, 0, run.score, Mathf.Clamp(run.score / 20000f, 0.6f, 1.4f)));
            seq.AppendCallback(() =>
            {
                score.rectTransform.DOPunchScale(Vector3.one * 0.12f, 0.3f, 6, 0.6f).SetUpdate(true);
                if (settlement.newBest)
                {
                    badge.gameObject.SetActive(true);
                    badge.localScale = Vector3.zero;
                    badge.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true);
                    badge.DORotate(new Vector3(0, 0, 4), 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetDelay(0.45f);
                    AudioService.Instance?.Play(Sfx.NewBest);
                    game.Juice.BloomPulse(1.2f, 0.8f);
                }
            });
            seq.Append(UIKit.Count(banked, 0, settlement.banked, 0.5f, v => "+" + UIKit.Num(v)));
            seq.AppendCallback(() =>
            {
                bankBox.DOPunchScale(Vector3.one * 0.06f, 0.3f, 6, 0.6f).SetUpdate(true);
                if (settlement.banked > 0) AudioService.Instance?.Play(Sfx.GoldPickup);
            });
        }

        public void SetDoubleBusy(bool busy)
        {
            doubleGold.SetInteractable(!busy);
            if (busy) doubleGold.SetLabel(Loc.T("ad_loading"));
        }

        public void ShowAdFailed()
        {
            doubleGold.SetLabel(Loc.T("ad_retry"));
            AudioService.Instance?.Play(Sfx.Denied);
        }

        public void ShowDoubled(int amount)
        {
            doubleGold.root.gameObject.SetActive(false);
            int from = bankedShown;
            bankedShown += amount;
            UIKit.Count(banked, from, bankedShown, 0.5f, v => "+" + UIKit.Num(v));
            bankBox.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f).SetUpdate(true);
            goals.Refresh();
            game.Juice.BloomPulse(1f, 0.6f);
        }

        protected override void OnHide()
        {
            badge.DOKill(); badge.localRotation = Quaternion.identity;
            planets.juice.transform.DOKill(); planets.juice.transform.localScale = Vector3.one;
        }
    }

    // What the player is working towards: the next planet and the three missions. Lives on the results screen and in the Goals card.
    public sealed class GoalsView
    {
        readonly GameManager game;
        readonly TextMeshProUGUI planetText, header;
        readonly Image planetFill;
        readonly (RectTransform root, TextMeshProUGUI title, TextMeshProUGUI progress, TextMeshProUGUI reward, Image fill)[] rows;

        public GoalsView(RectTransform parent, GameManager game, float width)
        {
            this.game = game;
            var box = UIKit.Rect(parent, "View");
            box.Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(width, 176));
            planetText = UIKit.Text(box, "Planet", "", 13, Palette.Gold, TextAlignmentOptions.Left, true, 1);
            planetText.rectTransform.Place(new Vector2(0, 1), Vector2.zero, new Vector2(width, 18));
            planetText.enableAutoSizing = true; planetText.fontSizeMin = 10; planetText.fontSizeMax = 13; planetText.textWrappingMode = TextWrappingModes.NoWrap;
            planetFill = Bar(box, new Vector2(0, -20), width, Palette.Gold);
            header = UIKit.Text(box, "Header", "", 11, Palette.TextDim, TextAlignmentOptions.Left, true, 2);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(0, -36), new Vector2(width, 16));
            rows = new (RectTransform, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, Image)[3];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = UIKit.Rect(box, "Row" + i);
                row.Place(new Vector2(0, 1), new Vector2(0, -54 - i * 41), new Vector2(width, 38));
                var title = UIKit.Text(row, "Title", "", 12, Palette.Text, TextAlignmentOptions.Left, false);
                title.rectTransform.Place(new Vector2(0, 1), Vector2.zero, new Vector2(width - 112, 18));
                title.enableAutoSizing = true; title.fontSizeMin = 9; title.fontSizeMax = 12; title.textWrappingMode = TextWrappingModes.NoWrap;
                var progress = UIKit.Text(row, "Progress", "", 12, Palette.TextDim, TextAlignmentOptions.Right, true);
                progress.rectTransform.Place(new Vector2(1, 1), new Vector2(-46, 0), new Vector2(64, 18));
                progress.enableAutoSizing = true; progress.fontSizeMin = 9; progress.fontSizeMax = 12; progress.textWrappingMode = TextWrappingModes.NoWrap;
                var reward = UIKit.Text(row, "Reward", "", 12, Palette.Gold, TextAlignmentOptions.Right, true);
                reward.rectTransform.Place(new Vector2(1, 1), Vector2.zero, new Vector2(44, 18));
                rows[i] = (row, title, progress, reward, Bar(row, new Vector2(0, -21), width, Palette.Plasma));
            }
        }

        static Image Bar(RectTransform parent, Vector2 position, float width, Color color)
        {
            var track = UIKit.Image(parent, "Track", UIKit.Assets.pill, Palette.Deep, true);
            track.rectTransform.Place(new Vector2(0, 1), position, new Vector2(width, 8));
            var fill = UIKit.Image(track.transform, "Fill", UIKit.Assets.pill, color, true);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        static void SetFill(Image fill, float fraction)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            fill.enabled = fraction > 0.01f;
        }

        public void Refresh()
        {
            var next = EconomyService.NextPlanet(game.Config);
            int bank = EconomyService.BankGold;
            if (next == null) { planetText.text = Loc.T("all_planets"); SetFill(planetFill, 1f); }
            else
            {
                string name = Loc.T("planet_" + next.id);
                bool ready = bank >= next.price;
                planetText.text = ready ? Loc.F("planet_ready", name) : Loc.F("planet_next", name, UIKit.Num(bank), UIKit.Num(next.price));
                planetText.color = ready ? Palette.Buy : Palette.Gold;
                SetFill(planetFill, (float)bank / Mathf.Max(1, next.price));
            }
            int done = MissionService.CompletedThisRun;
            header.text = done > 0 ? $"{Loc.T("missions")}  ·  <color={Palette.Tag(Palette.Gold)}>{Loc.F("missions_done", done, UIKit.Num(MissionService.RewardThisRun))}</color>" : Loc.T("missions");
            var active = MissionService.Active();
            for (int i = 0; i < rows.Length; i++)
            {
                bool on = i < active.Count;
                rows[i].root.gameObject.SetActive(on);
                if (!on) continue;
                rows[i].title.text = active[i].Title;
                rows[i].progress.text = active[i].Progress;
                rows[i].reward.text = "+" + UIKit.Num(active[i].reward);
                SetFill(rows[i].fill, active[i].Fraction);
            }
        }
    }

    public sealed class GoalsScreen : UIScreen
    {
        readonly RectTransform card;
        readonly GoalsView view;
        public GoalsScreen(Transform parent, GameManager game) : base(parent, "GoalsCard", game, true, 0.94f)
        {
            card = Modal(column, 340);
            var title = UIKit.Text(card, "Title", Loc.T("goals"), 24, Palette.Text, TextAlignmentOptions.Center, true, 4);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(320, 36));
            var holder = UIKit.Rect(card, "Holder");
            holder.Place(new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(322, 190));
            view = new GoalsView(holder, game, 310);
            var back = UIKit.Button(card, "Back", Loc.T("back"), ButtonStyle.Ghost, Palette.TextDim, new Vector2(302, 52), () => Hide());
            back.root.Place(new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(302, 52));
        }
        protected override void OnShow() { view.Refresh(); In(card, 0f, 40, 0.9f); }
    }

    // Daily reward: claim is one tap, an ad doubles it, and tomorrow's bigger amount is the reason to come back.
    public sealed class DailyScreen : UIScreen
    {
        readonly RectTransform card;
        readonly TextMeshProUGUI day, amount, tomorrow;
        readonly UIKit.ButtonParts claim, watch;
        readonly List<(Image bg, TextMeshProUGUI text)> cells = new List<(Image, TextMeshProUGUI)>();

        public DailyScreen(Transform parent, GameManager game) : base(parent, "Daily", game, true, 0.94f)
        {
            card = Modal(column, 374);
            var title = UIKit.Text(card, "Title", Loc.T("daily_title"), 24, Palette.Text, TextAlignmentOptions.Center, true, 3);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(330, 34));
            title.enableAutoSizing = true; title.fontSizeMin = 16; title.fontSizeMax = 24; title.textWrappingMode = TextWrappingModes.NoWrap;
            day = UIKit.Text(card, "Day", "", 14, Palette.Plasma, TextAlignmentOptions.Center, true, 2);
            day.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(330, 22));
            amount = UIKit.Text(card, "Amount", "", 46, Palette.Gold, TextAlignmentOptions.Center, true);
            amount.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(330, 56));
            var strip = UIKit.Rect(card, "Strip");
            strip.Place(new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(322, 42));
            for (int i = 0; i < 7; i++)
            {
                var bg = UIKit.Image(strip, "Cell" + i, UIKit.Assets.panel, Palette.Panel, true);
                bg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(i * 46.5f, 0), new Vector2(43, 42));
                var text = UIKit.Text(bg.transform, "Text", "", 11, Palette.TextDim, TextAlignmentOptions.Center, true);
                text.rectTransform.Stretch(2, 2, 0, 0);
                text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 12; text.textWrappingMode = TextWrappingModes.NoWrap;
                cells.Add((bg, text));
            }
            tomorrow = UIKit.Text(card, "Tomorrow", "", 13, Palette.TextDim, TextAlignmentOptions.Center, false);
            tomorrow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(330, 22));
            watch = UIKit.Button(card, "Watch", Loc.T("daily_double"), ButtonStyle.Buy, Palette.Buy, new Vector2(302, 58), game.WatchDailyDouble, 17);
            watch.root.Place(new Vector2(0.5f, 0), new Vector2(0, 84), new Vector2(302, 58));
            claim = UIKit.Button(card, "Claim", Loc.T("daily_claim"), ButtonStyle.Outline, Palette.Plasma, new Vector2(302, 52), game.ClaimDaily);
            claim.root.Place(new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(302, 52));
        }

        public UnityEngine.UI.Button ClaimButton => claim.button;

        protected override void OnShow()
        {
            int streak = DailyService.NextStreak;
            int position = (streak - 1) % 7;
            day.text = Loc.F("daily_day", streak);
            amount.text = "+" + UIKit.Num(DailyService.Reward(game.Config, streak));
            tomorrow.text = Loc.F("daily_next", UIKit.Num(DailyService.Reward(game.Config, streak + 1)));
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].text.text = UIKit.Num(game.Config.daily.rewards[i % game.Config.daily.rewards.Length]);
                cells[i].bg.color = i == position ? Palette.Gold : i < position ? Palette.Deep : Palette.Panel;
                cells[i].text.color = i == position ? Palette.Void : i < position ? Palette.Line : Palette.TextDim;
            }
            watch.root.gameObject.SetActive(game.RewardedAvailable);
            watch.SetLabel(Loc.T("daily_double"));
            In(card, 0f, 40, 0.9f);
        }

        public void ShowAdFailed()
        {
            watch.SetLabel(Loc.T("ad_retry"));
            AudioService.Instance?.Play(Sfx.Denied);
        }
    }
}
