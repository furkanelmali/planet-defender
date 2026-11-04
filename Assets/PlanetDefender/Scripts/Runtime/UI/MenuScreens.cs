using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetDefender
{
    // Main menu (design board "Main menu"): the selected planet floats in the world behind the UI.
    public sealed class MenuScreen : UIScreen
    {
        readonly RectTransform bankPill, titleBox, info;
        readonly TextMeshProUGUI bank, planetName, best;
        readonly UIKit.ButtonParts play, continueRun, planets, ranking, settings, language, goals;
        readonly RectTransform grid;

        public MenuScreen(Transform parent, GameManager game) : base(parent, "Menu", game)
        {
            bankPill = Pill(column, out bank);
            bankPill.Place(new Vector2(0, 1), new Vector2(24, -28), new Vector2(130, 40));
            settings = UIKit.IconButton(column, "Settings", UIKit.Assets.gear, new Vector2(44, 44), game.OpenSettings, "Settings");
            settings.root.Place(new Vector2(1, 1), new Vector2(-24, -26), new Vector2(44, 44));
            // Flag button: shows the current language, opens the picker.
            language = UIKit.IconButton(column, "Language", UIKit.Assets.flags[Mathf.Max(0, Loc.LanguageIndex)], new Vector2(56, 44), game.OpenLanguage, "Language");
            language.root.Place(new Vector2(1, 1), new Vector2(-78, -26), new Vector2(56, 44));
            language.icon.color = Color.white;
            language.icon.rectTransform.sizeDelta = new Vector2(36, 24);

            titleBox = UIKit.Rect(column, "Title");
            titleBox.Place(new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(390, 96));
            var a = UIKit.Text(titleBox, "A", Loc.T("title_a"), 40, Palette.Text, TextAlignmentOptions.Center, true, 6);
            a.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(390, 46));
            var b = UIKit.Text(titleBox, "B", Loc.T("title_b"), 40, Palette.Plasma, TextAlignmentOptions.Center, true, 6);
            b.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(390, 46));
            b.fontSizeMax = 40; b.enableAutoSizing = true; b.fontSizeMin = 24;
            a.enableAutoSizing = true; a.fontSizeMin = 24; a.fontSizeMax = 40;

            // The nearest goal, always in sight; tapping it opens the full list.
            goals = UIKit.Button(column, "Goals", "", ButtonStyle.Outline, Palette.Gold, new Vector2(342, 42), game.OpenGoals, 12);
            goals.root.Place(new Vector2(0.5f, 1), new Vector2(0, -204), new Vector2(342, 42));

            info = UIKit.Rect(column, "Info");
            info.Place(new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(342, 52));
            planetName = UIKit.Text(info, "Planet", "", 13, Palette.TextDim, TextAlignmentOptions.Center, true, 3);
            planetName.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(342, 22));
            best = UIKit.Text(info, "Best", "", 14, Palette.TextDim, TextAlignmentOptions.Center, true, 1);
            best.rectTransform.Place(new Vector2(0.5f, 0), Vector2.zero, new Vector2(342, 24));

            play = UIKit.Button(column, "Play", Loc.T("play"), ButtonStyle.Primary, Palette.Plasma, new Vector2(342, 64), game.Play);
            play.root.Place(new Vector2(0.5f, 0), new Vector2(0, 108), new Vector2(342, 64));
            continueRun = UIKit.Button(column, "Continue", Loc.T("continue"), ButtonStyle.Buy, Palette.Buy, new Vector2(342, 48), game.ContinueRun);
            continueRun.root.Place(new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(342, 48));

            grid = UIKit.Rect(column, "Grid");
            grid.Place(new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(342, 52));
            planets = UIKit.Button(grid, "Planets", Loc.T("planets"), ButtonStyle.Outline, Palette.Plasma, new Vector2(165, 52), game.OpenShop);
            ranking = UIKit.Button(grid, "Ranking", Loc.T("ranking"), ButtonStyle.Outline, Palette.Xp, new Vector2(165, 52), game.OpenLeaderboard);
        }

        public static RectTransform Pill(Transform parent, out TextMeshProUGUI value)
        {
            var pill = UIKit.Panel(parent, "Bank", Palette.Panel, Palette.Line);
            pill.sprite = UIKit.Assets.pill;
            pill.transform.GetChild(0).GetComponent<Image>().sprite = UIKit.Assets.pillOutline;
            var coin = UIKit.Image(pill.transform, "Coin", UIKit.Assets.coin, Palette.Gold);
            coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(18, 18));
            value = UIKit.Text(pill.transform, "Value", "0", 15, Palette.Gold, TextAlignmentOptions.Left, true, 0);
            value.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(120, 24));
            return pill.rectTransform;
        }

        // Also called after the daily reward lands on the bank while the menu stays open.
        public void Refresh()
        {
            var data = SaveService.Data;
            bank.text = UIKit.Num(data.bankGold);
            var mission = MissionService.Closest();
            goals.root.gameObject.SetActive(mission != null);
            if (mission != null) goals.SetLabel($"{mission.Title}   {mission.Progress}   +{UIKit.Num(mission.reward)}");
            bool canBuy = EconomyService.NextPlanet(game.Config) is PlanetConfig next && EconomyService.CanBuy(next);
            planets.juice.transform.DOKill();
            planets.juice.transform.localScale = Vector3.one;
            if (canBuy) planets.juice.transform.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        public void CelebrateBank(int amount)
        {
            bankPill.DOKill(true);
            bankPill.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f).SetUpdate(true);
        }

        protected override void OnShow()
        {
            var data = SaveService.Data;
            var planet = game.Config.Planet(data.selectedPlanet);
            Refresh();
            planetName.text = $"{Loc.T("planet_" + planet.id)}  ·  {Loc.F("lives", planet.hp)}";
            best.text = $"{Loc.T("best")}  <color={Palette.Tag(Palette.Text)}>{UIKit.Num(data.bestScore)}</color>";
            bool canContinue = game.HasResumableRun;
            continueRun.root.gameObject.SetActive(canContinue);
            const bool rank = true;
            ranking.root.gameObject.SetActive(rank);
            planets.root.Place(rank ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(rank ? 165 : 342, 52));
            ranking.root.Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(165, 52));

            In(titleBox, 0.05f, 30); In(bankPill, 0f, 10); In(settings.root, 0.02f, 10); In(language.root, 0.04f, 10);
            In(goals.root, 0.08f, 20); In(info, 0.12f); In(play.root, 0.16f); In(grid, 0.22f);
            if (canContinue) In(continueRun.root, 0.2f);
            // Idle float runs on the title's children so it never fights the entrance tween on the box itself.
            foreach (RectTransform child in titleBox)
            {
                child.DOKill();
                child.anchoredPosition = new Vector2(0, child.name == "A" ? 0 : -44);
                child.DOAnchorPosY(child.anchoredPosition.y + 6, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true).SetDelay(child.name == "A" ? 0.6f : 0.75f);
            }
            play.face.DOKill();
            play.juice.transform.DOScale(1.035f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetDelay(0.8f);
        }

        protected override void OnHide()
        {
            foreach (RectTransform child in titleBox) child.DOKill();
            play.juice.transform.DOKill(); play.juice.transform.localScale = Vector3.one;
            planets.juice.transform.DOKill(); planets.juice.transform.localScale = Vector3.one;
        }
    }

    // Planet Shop (GDD §4): browse the eight planets in the world view, buy with Bank Gold or select.
    public sealed class ShopScreen : UIScreen
    {
        readonly TextMeshProUGUI bank, title, lives, passive, price, note;
        readonly UIKit.ButtonParts action, left, right, back;
        readonly RectTransform card, lockBadge, dots;
        readonly List<Image> dotImages = new List<Image>();
        int index;

        public ShopScreen(Transform parent, GameManager game) : base(parent, "Shop", game)
        {
            var bankPill = MenuScreen.Pill(column, out bank);
            bankPill.Place(new Vector2(0, 1), new Vector2(24, -28), new Vector2(130, 40));
            back = UIKit.IconButton(column, "Back", UIKit.Assets.arrowLeft, new Vector2(44, 44), game.BackToMenu, "Back");
            back.root.Place(new Vector2(1, 1), new Vector2(-24, -26), new Vector2(44, 44));
            var heading = UIKit.Text(column, "Heading", Loc.T("planets"), 22, Palette.Text, TextAlignmentOptions.Center, true, 5);
            heading.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(390, 32));

            left = UIKit.IconButton(column, "Prev", UIKit.Assets.arrowLeft, new Vector2(48, 48), () => Step(-1), "Previous planet");
            left.root.Place(new Vector2(0, 0.5f), new Vector2(16, 70), new Vector2(48, 48));
            right = UIKit.IconButton(column, "Next", UIKit.Assets.arrowRight, new Vector2(48, 48), () => Step(1), "Next planet");
            right.root.Place(new Vector2(1, 0.5f), new Vector2(-16, 70), new Vector2(48, 48));

            lockBadge = UIKit.Rect(column, "Lock");
            lockBadge.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(54, 54));
            var lockBg = UIKit.Image(lockBadge, "Bg", UIKit.Assets.softCircle, Palette.Void.WithAlpha(0.8f)); lockBg.rectTransform.Stretch(-10, -10, -10, -10);
            var lockIcon = UIKit.Image(lockBadge, "Icon", UIKit.Assets.lockIcon, Palette.Text); lockIcon.rectTransform.Stretch(10, 10, 10, 10);

            card = UIKit.Panel(column, "Card", Palette.Panel.WithAlpha(0.92f), Palette.Line).rectTransform;
            card.Place(new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(342, 216));
            title = UIKit.Text(card, "Name", "", 24, Palette.Text, TextAlignmentOptions.Center, true, 4);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(320, 32));
            lives = UIKit.Text(card, "Lives", "", 14, Palette.Danger, TextAlignmentOptions.Center, true, 2);
            lives.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(320, 22));
            passive = UIKit.Text(card, "Passive", "", 15, Palette.Text, TextAlignmentOptions.Center, false);
            passive.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(300, 44));
            price = UIKit.Text(card, "Price", "", 13, Palette.TextDim, TextAlignmentOptions.Center, true, 1);
            price.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(320, 20));
            action = UIKit.Button(card, "Action", Loc.T("select"), ButtonStyle.Primary, Palette.Plasma, new Vector2(300, 48), OnAction);
            action.root.Place(new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(300, 48));

            dots = UIKit.Rect(column, "Dots");
            dots.Place(new Vector2(0.5f, 0), new Vector2(0, 332), new Vector2(200, 10));
            var h = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = false; h.childControlHeight = false;
            for (int i = 0; i < game.Config.planets.Length; i++)
            {
                var d = UIKit.Image(dots, "Dot", UIKit.Assets.softCircle, Palette.Line);
                d.rectTransform.sizeDelta = new Vector2(10, 10);
                dotImages.Add(d);
            }
            note = UIKit.Text(column, "Note", Loc.T("powers_note"), 11, Palette.TextDim, TextAlignmentOptions.Center, false);
            note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(330, 44));
            note.rectTransform.anchoredPosition = new Vector2(0, 48);
        }

        protected override void OnShow()
        {
            index = game.Config.PlanetIndex(SaveService.Data.selectedPlanet);
            Refresh(0);
            In(card, 0.05f, 60);
        }

        void Step(int delta)
        {
            int n = game.Config.planets.Length;
            index = (index + delta + n) % n;
            Refresh(delta);
        }

        public override void Tick()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
        }

        void Refresh(int direction)
        {
            var planet = game.Config.planets[index];
            var data = SaveService.Data;
            bool owned = EconomyService.IsUnlocked(planet.id);
            bool selected = data.selectedPlanet == planet.id;
            game.PreviewPlanet(planet, direction);
            bank.text = UIKit.Num(data.bankGold);
            title.text = Loc.T("planet_" + planet.id);
            int bonus = planet.hp - 3;
            lives.text = Loc.F("lives", planet.hp) + (bonus > 0 ? $"  (+{bonus})" : "");
            passive.text = Loc.T("passive_" + planet.passive);
            lockBadge.gameObject.SetActive(!owned);
            if (owned)
            {
                price.text = planet.price == 0 ? Loc.T("free") : "";
                action.root.gameObject.SetActive(true);
                action.style = ButtonStyle.Primary; action.color = Palette.Plasma;
                action.SetLabel(selected ? Loc.T("selected") : Loc.T("select"));
                action.SetCoin(false);
                action.SetInteractable(!selected);
            }
            else
            {
                price.text = Loc.T("locked");
                action.style = ButtonStyle.Buy; action.color = Palette.Buy;
                action.SetLabel($"{Loc.T("buy")}  {UIKit.Num(planet.price)}");
                action.SetCoin(true);
                action.SetInteractable(data.bankGold >= planet.price);
            }
            for (int i = 0; i < dotImages.Count; i++)
            {
                dotImages[i].color = i == index ? Palette.Plasma : Palette.Line;
                dotImages[i].rectTransform.DOKill();
                dotImages[i].rectTransform.DOScale(i == index ? 1.35f : 1f, 0.2f).SetUpdate(true);
            }
            if (direction != 0)
            {
                card.DOKill(true);
                card.DOPunchAnchorPos(new Vector2(-direction * 18, 0), 0.3f, 6, 0.6f).SetUpdate(true);
            }
            lockBadge.DOKill(true);
            if (!owned) lockBadge.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.6f).SetUpdate(true);
        }

        void OnAction()
        {
            var planet = game.Config.planets[index];
            if (EconomyService.IsUnlocked(planet.id)) { game.SelectPlanet(planet.id); Refresh(0); action.juice.Attention(); return; }
            if (game.BuyPlanet(planet))
            {
                Refresh(0);
                action.juice.Attention();
                card.DOKill(true);
                card.DOPunchScale(Vector3.one * 0.08f, 0.45f, 7, 0.6f).SetUpdate(true);
                int before = SaveService.Data.bankGold + planet.price;
                UIKit.Count(bank, before, SaveService.Data.bankGold, 0.6f);
            }
            else { action.juice.Deny(); AudioService.Instance?.Play(Sfx.Denied); }
        }
    }

    // Leaderboard (GDD §11.1): top entries from Bridge SaaS plus the player's best. No invented rivals ever.
    public sealed class LeaderboardScreen : UIScreen
    {
        readonly RectTransform list, statusBox;
        readonly TextMeshProUGUI status, mine;
        readonly UIKit.ButtonParts retry;
        System.Action onClose;

        public LeaderboardScreen(Transform parent, GameManager game) : base(parent, "Leaderboard", game, true, 0.94f)
        {
            card = Modal(column, 720);
            var title = UIKit.Text(card, "Title", Loc.T("leaderboard"), 22, Palette.Xp, TextAlignmentOptions.Center, true, 4);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(330, 32));
            var mineBox = UIKit.Panel(card, "Mine", Palette.Panel, Palette.Gold).rectTransform;
            mineBox.Place(new Vector2(0.5f, 1), new Vector2(0, -68), new Vector2(310, 52));
            mine = UIKit.Text(mineBox, "Value", "", 16, Palette.Gold, TextAlignmentOptions.Center, true, 2);
            mine.rectTransform.Stretch(12, 12, 0, 0);
            list = UIKit.Rect(card, "List");
            list.anchorMin = new Vector2(0.5f, 0); list.anchorMax = new Vector2(0.5f, 1); list.pivot = new Vector2(0.5f, 1);
            list.sizeDelta = new Vector2(310, -224); list.anchoredPosition = new Vector2(0, -132);
            UIKit.VStack(list, 4);
            statusBox = UIKit.Rect(card, "Status");
            statusBox.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(300, 150));
            status = UIKit.Text(statusBox, "Text", "", 15, Palette.TextDim, TextAlignmentOptions.Center, false);
            status.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(300, 90));
            retry = UIKit.Button(statusBox, "Retry", Loc.T("retry_small"), ButtonStyle.Outline, Palette.Plasma, new Vector2(200, 46), Load);
            retry.root.Place(new Vector2(0.5f, 0), Vector2.zero, new Vector2(200, 46));
            var back = UIKit.Button(card, "Back", Loc.T("back"), ButtonStyle.Ghost, Palette.TextDim, new Vector2(310, 52), () => { Hide(); onClose?.Invoke(); });
            back.root.Place(new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(310, 52));
        }

        readonly RectTransform card;

        public void Open(System.Action closed) { onClose = closed; Show(); }

        protected override void OnShow()
        {
            In(card, 0f, 40, 0.9f);
            mine.text = $"{Loc.T("your_best")}   {UIKit.Num(SaveService.Data.bestScore)}";
            Load();
        }

        void Load()
        {
            if (!LeaderboardService.HasGlobalList)
            {
                for (int i = list.childCount - 1; i >= 0; i--) Object.Destroy(list.GetChild(i).gameObject);
                statusBox.gameObject.SetActive(true);
                retry.root.gameObject.SetActive(false);
                status.text = Loc.T("lb_unavailable");
                return;
            }
            for (int i = list.childCount - 1; i >= 0; i--) Object.Destroy(list.GetChild(i).gameObject);
            statusBox.gameObject.SetActive(true);
            retry.root.gameObject.SetActive(false);
            status.text = Loc.T("lb_loading");
            LeaderboardService.GetEntries((ok, entries) =>
            {
                if (!Visible) return;
                if (!ok) { status.text = Loc.T("lb_error"); retry.root.gameObject.SetActive(true); return; }
                if (entries == null || entries.Count == 0) { status.text = Loc.T("lb_empty"); return; }
                statusBox.gameObject.SetActive(false);
                int shown = 0;
                foreach (var e in entries)
                {
                    if (shown >= 10 && !e.isPlayer) continue;
                    Row(e, shown);
                    shown++;
                }
            });
        }

        void Row(LeaderboardEntry e, int i)
        {
            var row = UIKit.Panel(list, "Row", e.isPlayer ? Palette.Deep : Palette.Panel, e.isPlayer ? Palette.Gold : (Color?)null);
            row.Size(38);
            var rank = UIKit.Text(row.transform, "Rank", "#" + e.rank, 15, e.rank <= 3 ? Palette.Gold : Palette.TextDim, TextAlignmentOptions.Left, true);
            rank.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(56, 30));
            var name = UIKit.Text(row.transform, "Name", e.isPlayer ? $"{e.name} ({Loc.T("rank_you")})" : e.name, 15, Palette.Text, TextAlignmentOptions.Left, false);
            name.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(170, 30));
            name.overflowMode = TextOverflowModes.Ellipsis; name.textWrappingMode = TextWrappingModes.NoWrap;
            var score = UIKit.Text(row.transform, "Score", UIKit.Num(e.score), 15, Palette.Text, TextAlignmentOptions.Right, true);
            score.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(110, 30));
            var cg = row.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1, 0.25f).SetDelay(i * 0.04f).SetUpdate(true);
        }
    }
}
