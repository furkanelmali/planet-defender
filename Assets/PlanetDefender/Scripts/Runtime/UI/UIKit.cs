using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlanetDefender
{
    public enum ButtonStyle { Primary, Outline, Buy, Ghost, Icon, Danger }

    // Builds uGUI in the units of the design board (390 × 844 reference, px = px), so sizes read straight off the
    // mockups. Every tween is unscaled: menus animate while gameplay time is frozen.
    public static class UIKit
    {
        public static GameAssets Assets;

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // Anchor + pivot at the same normalized point, then place by anchoredPosition.
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true, float spacing = 0)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Assets.font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public sealed class ButtonParts
        {
            public Button button;
            public RectTransform root;
            public Image face, lip;
            public TextMeshProUGUI label;
            public Image icon;
            public ButtonJuice juice;
            public ButtonStyle style;
            public Color color;
            public void SetLabel(string text) { if (label) label.text = text; }
            Image coin;
            // Gold price buttons carry a coin glyph at the left of the face; the label shifts right to make room.
            public void SetCoin(bool on)
            {
                if (on && coin == null)
                {
                    coin = Image(face.transform, "Coin", Assets.coin, Palette.Void);
                    coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(18, 18));
                }
                if (coin) coin.gameObject.SetActive(on);
                if (label) label.rectTransform.Stretch(on ? 34 : 8, 8, 0, 0);
            }
            public void SetInteractable(bool on)
            {
                button.interactable = on;
                Color faceColor = on ? (style == ButtonStyle.Outline || style == ButtonStyle.Ghost ? color : color) : Palette.Disabled;
                face.color = faceColor;
                if (lip) lip.color = on ? Lip(style, color) : Palette.Disabled * 0.7f;
                if (label) label.color = on ? LabelColor(style, color) : Palette.TextDim;
            }
        }

        public static ButtonParts Button(Transform parent, string name, string label, ButtonStyle style, Color color, Vector2 size, UnityAction onClick, float fontSize = 0)
        {
            var root = Rect(parent, name);
            root.sizeDelta = size;
            var parts = new ButtonParts { root = root, style = style, color = color };
            bool solid = style == ButtonStyle.Primary || style == ButtonStyle.Buy || style == ButtonStyle.Danger;
            float lipDepth = solid ? (size.y >= 56 ? 7 : 4) : 0;
            if (lipDepth > 0)
            {
                parts.lip = Image(root, "Lip", Assets.pill, Lip(style, color), true);
                parts.lip.rectTransform.Stretch(0, 0, lipDepth, -lipDepth);
            }
            var face = Image(root, "Face", solid || style == ButtonStyle.Icon ? Assets.pill : Assets.pillOutline,
                style == ButtonStyle.Icon ? Palette.Panel : color, true);
            face.rectTransform.Stretch();
            face.raycastTarget = true;
            parts.face = face;
            if (style == ButtonStyle.Icon)
            {
                var border = Image(face.transform, "Border", Assets.pillOutline, Palette.Line, true);
                border.rectTransform.Stretch();
            }
            // An empty string still gets a label so SetLabel can fill it later (settings rows); null means icon-only.
            if (label != null)
            {
                if (fontSize <= 0) fontSize = size.y >= 60 ? 22 : size.y >= 50 ? 14 : 13;
                parts.label = Text(face.transform, "Label", label, fontSize, LabelColor(style, color), TextAlignmentOptions.Center, true, solid ? 4 : 3);
                parts.label.rectTransform.Stretch(8, 8, 0, 0);
                parts.label.textWrappingMode = TextWrappingModes.NoWrap;
                parts.label.enableAutoSizing = true;
                parts.label.fontSizeMin = 9; parts.label.fontSizeMax = fontSize;
            }
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            if (onClick != null) button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => AudioService.Instance?.Play(Sfx.UiClick, 0.7f));
            parts.button = button;
            parts.juice = root.gameObject.AddComponent<ButtonJuice>();
            parts.juice.face = face.rectTransform;
            parts.juice.lipDepth = lipDepth;
            return parts;
        }

        public static ButtonParts IconButton(Transform parent, string name, Sprite icon, Vector2 size, UnityAction onClick, string accessibleName = null)
        {
            var parts = Button(parent, name, null, ButtonStyle.Icon, Palette.Panel, size, onClick);
            parts.icon = Image(parts.face.transform, "Icon", icon, Palette.Text);
            parts.icon.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.46f);
            parts.root.gameObject.name = accessibleName ?? name;
            return parts;
        }

        static Color Lip(ButtonStyle style, Color color) =>
            style == ButtonStyle.Primary ? Palette.PlasmaLip : style == ButtonStyle.Buy ? Palette.BuyLip : Color.Lerp(color, Color.black, 0.45f);
        static Color LabelColor(ButtonStyle style, Color color) =>
            style == ButtonStyle.Primary || style == ButtonStyle.Buy || style == ButtonStyle.Danger ? Palette.Void : style == ButtonStyle.Ghost ? Palette.Text : color;

        public static Image Panel(Transform parent, string name, Color fill, Color? border = null)
        {
            var img = Image(parent, name, Assets.panel, fill, true);
            if (border.HasValue)
            {
                var b = Image(img.transform, "Border", Assets.panelOutline, border.Value, true);
                b.rectTransform.Stretch();
            }
            return img;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperCenter, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align; v.padding = padding ?? new RectOffset();
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static LayoutElement Size(this Component c, float height = -1, float width = -1, float flexW = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        // One shared outlined variant of the font material (dark rim keeps popups readable over bright FX).
        static Material outlined;
        public static Material OutlinedFont(TMP_FontAsset font)
        {
            if (outlined != null) return outlined;
            outlined = new Material(font.material) { name = font.material.name + " (Outline)" };
            outlined.EnableKeyword("OUTLINE_ON");
            outlined.SetFloat("_OutlineWidth", 0.22f);
            outlined.SetColor("_OutlineColor", new Color32(6, 10, 24, 255));
            outlined.SetFloat("_FaceDilate", 0.1f);
            return outlined;
        }

        // TMP tweens are a DOTween Pro feature; the free version animates alpha through a getter/setter.
        public static Tween Fade(this TMP_Text text, float to, float duration) =>
            DOTween.To(() => text.alpha, v => text.alpha = v, to, duration).SetTarget(text);

        public static string Clock(int seconds) => $"{Mathf.Max(0, seconds) / 60}:{Mathf.Max(0, seconds) % 60:00}";
        public static string Num(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        // Counts a TMP number up/down to target (used for score, gold and results).
        public static Tween Count(TextMeshProUGUI text, int from, int to, float duration, Func<int, string> format = null)
        {
            format ??= Num;
            int shown = from;
            return DOTween.To(() => shown, v => { shown = v; text.text = format(v); }, to, duration).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(text);
        }
    }
}
