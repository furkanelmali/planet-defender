using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlanetDefender.EditorTools
{
    // Rasterises signed-distance shapes into anti-aliased white sprites (colour comes from the Image/Renderer tint).
    // Coordinates are in pixels with the origin at the texture centre, +y up.
    public static class SdfPainter
    {
        public delegate float Sdf(Vector2 p);

        public static Texture2D Paint(int width, int height, Sdf sdf, float soft = 1f)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f - height * 0.5f);
                float d = sdf(p);
                float a = Mathf.Clamp01(0.5f - d / soft);
                px[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // Full-colour layered artwork (flags): each layer is painted over the previous one, then the whole image is
        // clipped to a rounded rectangle.
        public static Texture2D PaintLayers(int width, int height, float cornerRadius, params (Sdf sdf, Color color)[] layers)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var px = new Color[width * height];
            var half = new Vector2(width * 0.5f, height * 0.5f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                var c = new Color(0, 0, 0, 0);
                foreach (var layer in layers)
                {
                    float a = Mathf.Clamp01(0.5f - layer.sdf(p));
                    c = new Color(Mathf.Lerp(c.r, layer.color.r, a), Mathf.Lerp(c.g, layer.color.g, a), Mathf.Lerp(c.b, layer.color.b, a), Mathf.Max(c.a, a));
                }
                c.a *= Mathf.Clamp01(0.5f - Box(p, Vector2.zero, half, cornerRadius));
                px[y * width + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static float Rotated(Vector2 p, float degrees, Func<Vector2, float> sdf)
        {
            float r = -degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return sdf(new Vector2(p.x * c - p.y * s, p.x * s + p.y * c));
        }

        // Radial falloff (no hard edge) for glows and particles.
        public static Texture2D Radial(int size, Func<float, float> falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                float a = Mathf.Clamp01(falloff(r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static void Save(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ---- primitives (all return signed distance in pixels)
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        public static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w * 0.5f;

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float radius = 0)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * radius;
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(ba.sqrMagnitude, 1e-6f));
            return (pa - ba * h).magnitude - width * 0.5f;
        }

        public static float Polyline(Vector2 p, float width, params Vector2[] pts)
        {
            float d = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Segment(p, pts[i], pts[i + 1], width));
            return d;
        }

        // Filled polygon (any simple polygon) via winding + edge distance.
        public static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = (p - v[0]).sqrMagnitude;
            float s = 1;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float Star(Vector2 p, int points, float outer, float inner, float rotation = 90f)
        {
            var v = new List<Vector2>();
            for (int i = 0; i < points * 2; i++)
            {
                float a = (rotation + i * 180f / points) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? outer : inner;
                v.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return Polygon(p, v.ToArray());
        }

        public static float Union(params float[] d) { float m = float.MaxValue; foreach (var x in d) m = Mathf.Min(m, x); return m; }
        public static float Subtract(float a, float b) => Mathf.Max(a, -b);

        public static TextureImporter Import(string path, bool sprite, Vector4 border = default, int ppu = 100, bool mip = false, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            imp.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            imp.spritePixelsPerUnit = ppu;
            imp.spriteBorder = border;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = mip;
            imp.wrapMode = wrap;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.sRGBTexture = true;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            return imp;
        }
    }
}
