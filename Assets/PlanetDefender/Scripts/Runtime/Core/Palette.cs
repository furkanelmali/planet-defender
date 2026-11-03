using UnityEngine;

namespace PlanetDefender
{
    // Tokens from the visual-language board: warm = threat, yellow = income, cyan = the player.
    public static class Palette
    {
        public static readonly Color Void = Hex(0x060A18);
        public static readonly Color Deep = Hex(0x0B1230);
        public static readonly Color Panel = Hex(0x111A3D);
        public static readonly Color Line = Hex(0x24305E);
        public static readonly Color Plasma = Hex(0x38E8FF);
        public static readonly Color PlasmaSoft = Hex(0x9BF6FF);
        public static readonly Color PlasmaLip = Hex(0x1A8FA6);
        public static readonly Color Xp = Hex(0xFF4FA3);
        public static readonly Color Gold = Hex(0xFFC93C);
        public static readonly Color GoldLight = Hex(0xFFE68A);
        public static readonly Color Meteor = Hex(0xFF6A3D);
        public static readonly Color Danger = Hex(0xFF3355);
        public static readonly Color Buy = Hex(0x5CFFB0);
        public static readonly Color BuyLip = Hex(0x2C9E6A);
        public static readonly Color Text = Hex(0xEAF1FF);
        public static readonly Color TextDim = Hex(0x93A3CF);
        public static readonly Color Disabled = Hex(0x24305E);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
        public static Color Hdr(this Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
        public static string Tag(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
