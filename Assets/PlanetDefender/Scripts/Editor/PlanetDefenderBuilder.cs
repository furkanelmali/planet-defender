using System;
using System.Collections.Generic;
using System.IO;
using PlanetDefender.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static PlanetDefender.EditorTools.SdfPainter;

namespace PlanetDefender.EditorTools
{
    // Generates every derived asset of the game from code: URP setup, procedural UI/FX sprites, materials,
    // particle prefabs, the audio library, the post-processing profile, GameAssets and the Game scene.
    // Menu: Planet Defender → Build Everything. Batch: -executeMethod PlanetDefender.EditorTools.PlanetDefenderBuilder.BatchBuild
    public static class PlanetDefenderBuilder
    {
        const string Root = "Assets/PlanetDefender";
        const string Gen = Root + "/Art/Generated";
        const string Planets = Root + "/Art/Planets";
        const string Mats = Root + "/Materials";
        const string VfxDir = Root + "/Prefabs/VFX";
        const string Settings = Root + "/Settings";
        const string Audio = Root + "/Audio";
        public const string ScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Planet Defender/Build Everything")]
        public static void BuildAll()
        {
            SetupProject();
            BuildTextures();
            var mats = BuildMaterials();
            var vfx = BuildVfx(mats);
            var audio = BuildAudio();
            var profile = BuildVolume();
            var assets = BuildGameAssets(mats, vfx, audio, profile);
            BuildScene(assets, profile);
            AssetDatabase.SaveAssets();
            Debug.Log("PD_BUILD_OK");
        }

        public static void BatchBuild()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ project

        [MenuItem("Planet Defender/Setup Project (URP, WebGL)")]
        public static void SetupProject()
        {
            PlayerSettings.companyName = "Furkan Elmali";
            PlayerSettings.productName = "Planet Defender";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = false;
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = player.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; player.ApplyModifiedPropertiesWithoutUndo(); }
            PlaygamaWebBuild.Configure();

            Directory.CreateDirectory(Settings);
            const string rendererPath = Settings + "/PD_Renderer.asset", pipelinePath = Settings + "/PD_URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 0;
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ textures

        static string G(string name) => $"{Gen}/{name}.png";

        [MenuItem("Planet Defender/Rebuild Textures")]
        public static void BuildTextures()
        {
            Directory.CreateDirectory(Gen);
            Save(new Texture2D(4, 4).Fill(), G("quad"));
            Save(Radial(128, r => Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f)), G("soft_circle"));
            Save(Radial(256, r => (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2)) + Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.18f, 2)) * 0.25f) * Mathf.Clamp01((0.98f - r) / 0.12f)), G("ring"));
            Save(Paint(128, 128, p => Star(p, 4, 62, 16)), G("star4"));
            Save(Paint(32, 128, p => Segment(p, new Vector2(0, -50), new Vector2(0, 50), 14), 6f), G("bolt"));
            Save(Paint(64, 32, p => Segment(p, new Vector2(-24, 0), new Vector2(24, 0), 10), 6f), G("spark"));
            Save(Paint(64, 64, p => Union(Box(p, new Vector2(0, 8), new Vector2(9, 20), 6), Circle(p, new Vector2(0, -14), 15))), G("turret"));
            Save(Paint(128, 128, p => Union(Ring(p, Vector2.zero, 40, 6),
                Box(p, new Vector2(0, 54), new Vector2(3, 10), 3), Box(p, new Vector2(0, -54), new Vector2(3, 10), 3),
                Box(p, new Vector2(54, 0), new Vector2(10, 3), 3), Box(p, new Vector2(-54, 0), new Vector2(10, 3), 3), Circle(p, Vector2.zero, 4))), G("crosshair"));
            Save(Paint(96, 96, p => Polygon(p, new Vector2(-30, -10), new Vector2(-6, -34), new Vector2(26, -24), new Vector2(34, 8), new Vector2(10, 32), new Vector2(-24, 22))), G("chip"));

            // UI: 9-slice panels and pills, icons drawn at 128 px.
            Save(Paint(64, 64, p => Box(p, Vector2.zero, new Vector2(32, 32), 18)), G("panel"));
            Save(Paint(64, 64, p => Mathf.Abs(Box(p, Vector2.zero, new Vector2(31, 31), 17)) - 1f), G("panel_outline"));
            Save(Paint(64, 64, p => Box(p, Vector2.zero, new Vector2(32, 32), 16)), G("pill"));
            Save(Paint(64, 64, p => Mathf.Abs(Box(p, Vector2.zero, new Vector2(31, 31), 15)) - 1f), G("pill_outline"));
            Save(Paint(128, 128, Heart), G("heart"));
            Save(Paint(128, 128, p => Mathf.Abs(Heart(p)) - 5f), G("heart_empty"));
            Save(Paint(128, 128, p => Star(p, 4, 60, 20)), G("coin"));
            Save(Paint(128, 128, p => Union(Box(p, new Vector2(-22, 0), new Vector2(12, 42), 5), Box(p, new Vector2(22, 0), new Vector2(12, 42), 5))), G("pause"));
            Save(Paint(128, 128, Gear), G("gear"));
            Save(Paint(128, 128, p => Union(Box(p, new Vector2(0, -18), new Vector2(38, 30), 8), Subtract(Ring(p, new Vector2(0, 16), 26, 12), Box(p, new Vector2(0, -6), new Vector2(60, 20))))), G("lock"));
            Save(Paint(128, 128, p => Polyline(p, 14, new Vector2(18, 40), new Vector2(-22, 0), new Vector2(18, -40))), G("arrow_left"));
            Save(Paint(128, 128, p => Polyline(p, 14, new Vector2(-18, 40), new Vector2(22, 0), new Vector2(-18, -40))), G("arrow_right"));
            Save(Paint(128, 128, p => Union(Circle(p, new Vector2(-24, -34), 16), Circle(p, new Vector2(30, -24), 16),
                Segment(p, new Vector2(-10, -34), new Vector2(-10, 40), 9), Segment(p, new Vector2(44, -24), new Vector2(44, 48), 9),
                Segment(p, new Vector2(-10, 40), new Vector2(44, 48), 12))), G("music"));
            Save(Paint(128, 128, p => Union(Polygon(p, new Vector2(-46, -16), new Vector2(-22, -16), new Vector2(6, -40), new Vector2(6, 40), new Vector2(-22, 16), new Vector2(-46, 16)),
                Subtract(Ring(p, new Vector2(6, 0), 30, 9), Box(p, new Vector2(-30, 0), new Vector2(36, 60))))), G("sound"));
            Save(Paint(128, 128, p => Union(Subtract(Box(p, new Vector2(0, 18), new Vector2(34, 32), 14), Box(p, new Vector2(0, 50), new Vector2(40, 12))),
                Box(p, new Vector2(0, -26), new Vector2(7, 16)), Box(p, new Vector2(0, -46), new Vector2(26, 7), 3))), G("trophy"));
            Save(Paint(128, 128, p => Polygon(p, new Vector2(-28, -40), new Vector2(42, 0), new Vector2(-28, 40))), G("play"));
            Save(Paint(128, 128, p => Polygon(p, new Vector2(0, 52), new Vector2(42, 34), new Vector2(38, -10), new Vector2(0, -52), new Vector2(-38, -10), new Vector2(-42, 34))), G("shield_icon"));

            // Upgrade icons, same order as GameConfig.upgrades.
            Save(Paint(128, 128, p => Polygon(p, new Vector2(14, 58), new Vector2(-30, -6), new Vector2(-2, -6), new Vector2(-14, -58), new Vector2(32, 8), new Vector2(4, 8))), G("up_rapid_fire"));
            Save(Paint(128, 128, p => Union(Segment(p, new Vector2(-18, -44), new Vector2(-18, 44), 18), Segment(p, new Vector2(18, -44), new Vector2(18, 44), 18))), G("up_double_shot"));
            Save(Paint(128, 128, p => Union(Ring(p, new Vector2(4, 0), 26, 9), Segment(p, new Vector2(-54, 0), new Vector2(40, 0), 10),
                Polygon(p, new Vector2(36, 18), new Vector2(60, 0), new Vector2(36, -18)))), G("up_piercing"));
            Save(Paint(128, 128, p => Union(Polyline(p, 13, new Vector2(-38, -6), new Vector2(0, 30), new Vector2(38, -6)), Polyline(p, 13, new Vector2(-38, -40), new Vector2(0, -4), new Vector2(38, -40)))), G("up_damage_boost"));
            Save(Paint(128, 128, p => Star(p, 8, 60, 28, 22.5f)), G("up_explosion"));
            Save(Paint(128, 128, Snowflake), G("up_freeze_pulse"));
            Save(Paint(128, 128, p => Union(Circle(p, Vector2.zero, 20), Subtract(Ring(p, Vector2.zero, 46, 7), Circle(p, new Vector2(33, 33), 16)), Circle(p, new Vector2(33, 33), 11))), G("up_orbital_drone"));
            Save(Paint(128, 128, p => Union(Box(p, new Vector2(10, 0), new Vector2(50, 7), 7), Polygon(p, new Vector2(-58, 0), new Vector2(-36, 22), new Vector2(-14, 0), new Vector2(-36, -22)))), G("up_laser_beam"));

            BuildFlags();
            BuildUranusMap();
            AssetDatabase.Refresh();
            foreach (var name in new[] { "quad", "soft_circle", "ring", "star4", "bolt", "spark", "turret", "crosshair", "chip", "heart", "heart_empty", "coin", "pause", "gear", "lock",
                         "arrow_left", "arrow_right", "music", "sound", "trophy", "play", "shield_icon",
                         "up_rapid_fire", "up_double_shot", "up_piercing", "up_damage_boost", "up_explosion", "up_freeze_pulse", "up_orbital_drone", "up_laser_beam" })
                Import(G(name), true, default, WorldPpu(name));
            foreach (var name in new[] { "panel", "panel_outline", "pill", "pill_outline" }) Import(G(name), true, new Vector4(20, 20, 20, 20), 100);
            foreach (var code in Loc.Languages) Import(G("flag_" + code), true, default, 100);

            foreach (var file in Directory.GetFiles(Planets, "*.*"))
            {
                if (file.EndsWith(".meta")) continue;
                var imp = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 1024;
                imp.wrapModeU = TextureWrapMode.Repeat;
                imp.wrapModeV = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 2;
                imp.sRGBTexture = true;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
            }
        }

        // World sprites are exactly one unit across so code scales them by world size; UI icons keep 100 ppu.
        static int WorldPpu(string name) => name switch
        {
            "quad" => 4, "soft_circle" => 128, "ring" => 256, "star4" => 128, "bolt" => 128, "spark" => 64,
            "turret" => 64, "crosshair" => 128, "chip" => 96, _ => 100
        };

        static Texture2D Fill(this Texture2D t)
        {
            var px = new Color32[t.width * t.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static float Heart(Vector2 p) =>
            Union(Circle(p, new Vector2(-22, 14), 26), Circle(p, new Vector2(22, 14), 26),
                Polygon(p, new Vector2(-46, 4), new Vector2(46, 4), new Vector2(0, -48)));

        static float Gear(Vector2 p)
        {
            float a = Mathf.Atan2(p.y, p.x);
            float teeth = Mathf.Clamp(Mathf.Cos(a * 8) * 3f, -1, 1) * 7f;
            float body = p.magnitude - (40 + teeth);
            return Subtract(body, Circle(p, Vector2.zero, 16));
        }

        static float Snowflake(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, Segment(p, -dir * 56, dir * 56, 10));
                for (int s = -1; s <= 1; s += 2)
                {
                    var tip = dir * 38 * s;
                    var n = new Vector2(-dir.y, dir.x);
                    d = Mathf.Min(d, Segment(p, tip, tip + (dir * s + n) * 13, 8), Segment(p, tip, tip + (dir * s - n) * 13, 8));
                }
            }
            return d;
        }

        // Language flags, same order as Loc.Languages (en, tr, ru, es). 96 × 64 with rounded corners.
        static void BuildFlags()
        {
            const int w = 96, h = 64;
            Color C(int rgb) => Palette.Hex(rgb);
            SdfPainter.Sdf all = p => Box(p, Vector2.zero, new Vector2(w, h));
            // United Kingdom (English): saltires under the St George cross.
            Save(PaintLayers(w, h, 8,
                (all, C(0x012169)),
                (p => Union(Segment(p, new Vector2(-48, -32), new Vector2(48, 32), 13), Segment(p, new Vector2(-48, 32), new Vector2(48, -32), 13)), Color.white),
                (p => Union(Segment(p, new Vector2(-48, -32), new Vector2(48, 32), 4.5f), Segment(p, new Vector2(-48, 32), new Vector2(48, -32), 4.5f)), C(0xC8102E)),
                (p => Union(Box(p, Vector2.zero, new Vector2(48, 10)), Box(p, Vector2.zero, new Vector2(10, 32))), Color.white),
                (p => Union(Box(p, Vector2.zero, new Vector2(48, 6)), Box(p, Vector2.zero, new Vector2(6, 32))), C(0xC8102E))), G("flag_en"));
            // Türkiye: white crescent and star on red.
            Save(PaintLayers(w, h, 8,
                (all, C(0xE30A17)),
                (p => Subtract(Circle(p, new Vector2(-12, 0), 16), Circle(p, new Vector2(-8, 0), 12.8f)), Color.white),
                (p => Rotated(p - new Vector2(10, 0), -90, q => Star(q, 5, 9, 3.6f)), Color.white)), G("flag_tr"));
            // Russia: white, blue, red bands.
            Save(PaintLayers(w, h, 8,
                (all, Color.white),
                (p => Box(p, new Vector2(0, 0), new Vector2(48, 10.67f)), C(0x0039A6)),
                (p => Box(p, new Vector2(0, -21.33f), new Vector2(48, 10.67f)), C(0xD52B1E))), G("flag_ru"));
            // Spain: red, double-height yellow, red.
            Save(PaintLayers(w, h, 8,
                (all, C(0xAA151B)),
                (p => Box(p, Vector2.zero, new Vector2(48, 16)), C(0xF1BF00))), G("flag_es"));
        }

        // Uranus has no usable public map in the project and is close to featureless in reality: pale cyan with
        // faint latitude bands and a brighter polar cap.
        static void BuildUranusMap()
        {
            string path = Planets + "/uranus_generated.png";
            const int w = 1024, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            var baseColor = new Color(0.62f, 0.86f, 0.88f);
            for (int y = 0; y < h; y++)
            {
                float lat = (y / (float)(h - 1)) * 2f - 1f;
                float bands = Mathf.Sin(lat * 24f) * 0.018f + Mathf.Sin(lat * 9f + 1.2f) * 0.025f;
                float capT = Mathf.Clamp01((lat - 0.55f) / 0.4f);
                float cap = capT * capT * (3f - 2f * capT) * 0.08f;
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.01f, y * 0.06f) * 0.03f;
                    var c = baseColor * (1f + bands + n + cap);
                    c = Color.Lerp(c, new Color(0.55f, 0.80f, 0.85f), Mathf.Abs(lat) < 0.15f ? 0.12f : 0f);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(G(name));
        static Texture2D T(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(G(name));

        // ------------------------------------------------------------------ materials

        public sealed class MaterialSet
        {
            public Material planet, ring, glow, meteor, gold, bolt, shield, laser, starfield, reticle;
            public Material vfxGlow, vfxSpark, vfxStar, vfxRing, vfxSmoke, vfxDebris;
        }

        static Material Mat(string name, string shader, Action<Material> setup = null)
        {
            Directory.CreateDirectory(Mats);
            string path = $"{Mats}/{name}.mat";
            var s = Shader.Find(shader);
            if (s == null) throw new InvalidOperationException("Shader not found: " + shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static MaterialSet BuildMaterials()
        {
            return new MaterialSet
            {
                planet = Mat("Planet", "PD/Planet"),
                ring = Mat("PlanetRing", "PD/PlanetRing"),
                glow = Mat("Glow", "PD/Glow", m => m.SetFloat("_Intensity", 1.6f)),
                meteor = Mat("Meteor", "PD/Meteor"),
                gold = Mat("Gold", "PD/Gold"),
                bolt = Mat("Bolt", "PD/Glow", m => m.SetFloat("_Intensity", 3.4f)),
                shield = Mat("Shield", "PD/Shield"),
                laser = Mat("Laser", "PD/Laser", m => m.SetColor("_Color", Palette.Xp)),
                starfield = Mat("Starfield", "PD/Starfield"),
                reticle = Mat("Reticle", "PD/Glow", m => m.SetFloat("_Intensity", 1.8f)),
                vfxGlow = Mat("VfxGlow", "PD/Glow", m => { m.EnableKeyword("PD_PARTICLE"); m.SetTexture("_MainTex", T("soft_circle")); m.SetFloat("_Intensity", 3f); }),
                vfxSpark = Mat("VfxSpark", "PD/Glow", m => { m.EnableKeyword("PD_PARTICLE"); m.SetTexture("_MainTex", T("spark")); m.SetFloat("_Intensity", 4f); }),
                vfxStar = Mat("VfxStar", "PD/Glow", m => { m.EnableKeyword("PD_PARTICLE"); m.SetTexture("_MainTex", T("star4")); m.SetFloat("_Intensity", 3f); }),
                vfxRing = Mat("VfxRing", "PD/Glow", m => { m.EnableKeyword("PD_PARTICLE"); m.SetTexture("_MainTex", T("ring")); m.SetFloat("_Intensity", 2.6f); }),
                vfxSmoke = Mat("VfxSmoke", "PD/Alpha", m => { m.SetTexture("_MainTex", T("soft_circle")); m.SetFloat("_Intensity", 1f); }),
                vfxDebris = Mat("VfxDebris", "PD/Alpha", m => { m.SetTexture("_MainTex", T("chip")); m.SetFloat("_Intensity", 1f); }),
            };
        }

        // ------------------------------------------------------------------ particles

        public sealed class VfxSet
        {
            public ParticleSystem explosionSmall, explosionBig, hitSpark, goldPickup, goldShatter, planetHit, shockwave, muzzle,
                freezePulse, shieldBreak, levelUpBurst, meteorTrail, goldSparkle, debris;
        }

        static ParticleSystem NewPS(string name, Transform parent, Material mat, bool stretch = false, int order = 20)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 0.6f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.startRotation = 0;
            var em = ps.emission; em.rateOverTime = 0;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.05f; shape.radiusThickness = 1;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.06f; r.lengthScale = 1.2f; }
            return ps;
        }

        static void Burst(ParticleSystem ps, int min, int max, float time = 0)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)min, (short)max) });
        }

        static ParticleSystem.MinMaxGradient Fade(Color a, Color b, float midAlpha = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(midAlpha, 0.5f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static void ColorLife(ParticleSystem ps, Color a, Color b, float mid = 1f)
        {
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(a, b, mid);
        }

        static void SizeLife(ParticleSystem ps, float start, float end, float peak = -1)
        {
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            var curve = peak < 0 ? AnimationCurve.Linear(0, start, 1, end) : new AnimationCurve(new Keyframe(0, start), new Keyframe(0.25f, peak), new Keyframe(1, end));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static void Drag(ParticleSystem ps, float drag)
        {
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = drag; lim.dampen = 0; lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = true;
        }

        static void Cone(ParticleSystem ps, float angle, float radius = 0.02f)
        {
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = angle; shape.radius = radius; shape.rotation = new Vector3(0, 90, 0);
        }

        static ParticleSystem SavePrefab(ParticleSystem root, string name)
        {
            Directory.CreateDirectory(VfxDir);
            string path = $"{VfxDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<ParticleSystem>();
        }

        static readonly Color Hot = new Color(1f, 0.9f, 0.55f), Fire = new Color(1f, 0.45f, 0.12f), Ember = new Color(0.8f, 0.12f, 0.04f);

        static ParticleSystem Explosion(MaterialSet m, string name, float scale)
        {
            var root = NewPS(name, null, m.vfxGlow);
            var main = root.main;
            main.startLifetime = 0.16f; main.startSpeed = 0; main.startSize = 1.6f * scale; main.startColor = Hot;
            Burst(root, 1, 1);
            ColorLife(root, Hot, Fire); SizeLife(root, 0.6f, 1.2f);

            var sparks = NewPS("Sparks", root.transform, m.vfxSpark, true);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * scale, 8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hot, Fire);
            Burst(sparks, (short)(18 * scale), (short)(28 * scale));
            ColorLife(sparks, Hot, Ember); SizeLife(sparks, 1f, 0f); Drag(sparks, 0.12f);

            var embers = NewPS("Embers", root.transform, m.vfxGlow);
            main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f * scale, 0.32f * scale); main.startColor = Fire;
            var shape = embers.shape; shape.radius = 0.25f * scale;
            Burst(embers, (short)(4 * scale), (short)(7 * scale));
            ColorLife(embers, Fire, Ember, 0.8f); SizeLife(embers, 1f, 0.1f); Drag(embers, 0.08f);

            var smoke = NewPS("Smoke", root.transform, m.vfxSmoke, false, 18);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.1f * scale);
            main.startColor = new Color(0.22f, 0.18f, 0.17f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            shape = smoke.shape; shape.radius = 0.2f * scale;
            Burst(smoke, (short)(5 * scale), (short)(7 * scale));
            ColorLife(smoke, new Color(0.3f, 0.25f, 0.23f), new Color(0.08f, 0.08f, 0.1f), 0.6f); SizeLife(smoke, 0.6f, 1.4f);

            if (scale > 1.2f)
            {
                var ring = NewPS("Shock", root.transform, m.vfxRing);
                main = ring.main;
                main.startLifetime = 0.42f; main.startSpeed = 0; main.startSize = 1f; main.startColor = Fire;
                Burst(ring, 1, 1);
                SizeLife(ring, 0.4f * scale, 4.5f * scale);
                ColorLife(ring, Hot, Fire);
            }
            return SavePrefab(root, name);
        }

        static VfxSet BuildVfx(MaterialSet m)
        {
            var set = new VfxSet();
            set.explosionSmall = Explosion(m, "ExplosionSmall", 1f);
            set.explosionBig = Explosion(m, "ExplosionBig", 1.8f);

            var hit = NewPS("HitSpark", null, m.vfxSpark, true);
            var main = hit.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Palette.PlasmaSoft);
            Cone(hit, 38); Burst(hit, 5, 8); ColorLife(hit, Color.white, Palette.Meteor); SizeLife(hit, 1, 0); Drag(hit, 0.15f);
            set.hitSpark = SavePrefab(hit, "HitSpark");

            var gp = NewPS("GoldPickup", null, m.vfxGlow);
            main = gp.main; main.startLifetime = 0.2f; main.startSpeed = 0; main.startSize = 2.2f; main.startColor = Palette.Gold;
            Burst(gp, 1, 1); ColorLife(gp, Palette.GoldLight, Palette.Gold); SizeLife(gp, 0.5f, 1.2f);
            var gs = NewPS("Sparks", gp.transform, m.vfxSpark, true);
            main = gs.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f); main.startColor = new ParticleSystem.MinMaxGradient(Palette.GoldLight, Palette.Gold);
            Burst(gs, 16, 22); ColorLife(gs, Palette.GoldLight, Palette.Gold); SizeLife(gs, 1, 0); Drag(gs, 0.1f);
            var gstars = NewPS("Stars", gp.transform, m.vfxStar);
            main = gstars.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f); main.startColor = Palette.GoldLight;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI);
            Burst(gstars, 6, 9); ColorLife(gstars, Color.white, Palette.Gold); SizeLife(gstars, 0.3f, 0f, 1f);
            var gring = NewPS("Ring", gp.transform, m.vfxRing);
            main = gring.main; main.startLifetime = 0.35f; main.startSpeed = 0; main.startSize = 1; main.startColor = Palette.Gold;
            Burst(gring, 1, 1); SizeLife(gring, 0.4f, 2.6f); ColorLife(gring, Palette.GoldLight, Palette.Gold);
            set.goldPickup = SavePrefab(gp, "GoldPickup");

            var gsh = NewPS("GoldShatter", null, m.vfxStar);
            main = gsh.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f); main.startColor = Palette.Gold;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var rot = gsh.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            Burst(gsh, 12, 16); ColorLife(gsh, Palette.GoldLight, new Color(0.5f, 0.35f, 0.1f), 0.8f); SizeLife(gsh, 1, 0.2f); Drag(gsh, 0.12f);
            var gpuff = NewPS("Puff", gsh.transform, m.vfxSmoke, false, 18);
            main = gpuff.main; main.startLifetime = 0.6f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startColor = new Color(0.45f, 0.42f, 0.38f, 0.5f);
            Burst(gpuff, 4, 6); SizeLife(gpuff, 0.6f, 1.3f); ColorLife(gpuff, new Color(0.5f, 0.45f, 0.4f), new Color(0.2f, 0.2f, 0.22f), 0.5f);
            set.goldShatter = SavePrefab(gsh, "GoldShatter");

            var ph = NewPS("PlanetHit", null, m.vfxSpark, true);
            main = ph.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f); main.startColor = new ParticleSystem.MinMaxGradient(Hot, Palette.Danger);
            Cone(ph, 55, 0.2f); Burst(ph, 26, 36); ColorLife(ph, Hot, Palette.Danger); SizeLife(ph, 1, 0); Drag(ph, 0.1f);
            var phf = NewPS("Flash", ph.transform, m.vfxGlow);
            main = phf.main; main.startLifetime = 0.22f; main.startSpeed = 0; main.startSize = 3f; main.startColor = Palette.Danger;
            Burst(phf, 1, 1); SizeLife(phf, 0.4f, 1.3f); ColorLife(phf, Hot, Palette.Danger);
            set.planetHit = SavePrefab(ph, "PlanetHit");

            var sw = NewPS("Shockwave", null, m.vfxRing);
            main = sw.main; main.startLifetime = 0.45f; main.startSpeed = 0; main.startSize = 1f; main.startColor = Color.white;
            Burst(sw, 1, 1); SizeLife(sw, 0.2f, 2.4f); ColorLife(sw, Color.white, Color.white);
            set.shockwave = SavePrefab(sw, "Shockwave");

            var mz = NewPS("Muzzle", null, m.vfxGlow, false, 22);
            main = mz.main; main.startLifetime = 0.07f; main.startSpeed = 0; main.startSize = 0.7f; main.startColor = Palette.PlasmaSoft;
            Burst(mz, 1, 1); SizeLife(mz, 1f, 0.3f);
            var mzs = NewPS("Sparks", mz.transform, m.vfxSpark, true, 22);
            main = mzs.main; main.startLifetime = 0.1f; main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f); main.startSize = 0.08f; main.startColor = Palette.PlasmaSoft;
            Cone(mzs, 18); Burst(mzs, 2, 3); SizeLife(mzs, 1, 0);
            set.muzzle = SavePrefab(mz, "Muzzle");

            var fz = NewPS("FreezePulse", null, m.vfxRing);
            main = fz.main; main.startLifetime = 0.8f; main.startSpeed = 0; main.startSize = 1f; main.startColor = Palette.PlasmaSoft; main.duration = 1f;
            Burst(fz, 1, 1); SizeLife(fz, 1.5f, 22f); ColorLife(fz, Color.white, Palette.Plasma, 0.8f);
            var fzs = NewPS("Frost", fz.transform, m.vfxStar);
            main = fzs.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f); main.startColor = Palette.PlasmaSoft;
            var fshape = fzs.shape; fshape.radius = 1.4f;
            Burst(fzs, 30, 40); ColorLife(fzs, Color.white, Palette.Plasma); SizeLife(fzs, 1, 0); Drag(fzs, 0.05f);
            set.freezePulse = SavePrefab(fz, "FreezePulse");

            var sb = NewPS("ShieldBreak", null, m.vfxRing);
            main = sb.main; main.startLifetime = 0.4f; main.startSpeed = 0; main.startSize = 1; main.startColor = Color.white;
            Burst(sb, 1, 1); SizeLife(sb, 2.6f, 4.2f); ColorLife(sb, Color.white, Color.white);
            var sbs = NewPS("Shards", sb.transform, m.vfxSpark, true);
            main = sbs.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f); main.startSize = 0.14f; main.startColor = Color.white;
            var sbshape = sbs.shape; sbshape.radius = 1.3f;
            Burst(sbs, 24, 30); SizeLife(sbs, 1, 0); Drag(sbs, 0.1f);
            set.shieldBreak = SavePrefab(sb, "ShieldBreak");

            var lu = NewPS("LevelUpBurst", null, m.vfxRing);
            main = lu.main; main.startLifetime = 0.6f; main.startSpeed = 0; main.startSize = 1; main.startColor = Color.white; main.useUnscaledTime = true;
            Burst(lu, 1, 1); SizeLife(lu, 1.5f, 9f); ColorLife(lu, Color.white, Color.white);
            var lus = NewPS("Sparks", lu.transform, m.vfxSpark, true);
            main = lus.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f); main.startColor = Color.white; main.useUnscaledTime = true;
            var lushape = lus.shape; lushape.radius = 1.2f;
            Burst(lus, 36, 48); SizeLife(lus, 1, 0); Drag(lus, 0.08f);
            var lust = NewPS("Stars", lu.transform, m.vfxStar);
            main = lust.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f); main.startColor = Color.white; main.useUnscaledTime = true;
            var lustshape = lust.shape; lustshape.radius = 1.4f;
            Burst(lust, 10, 14); SizeLife(lust, 0.2f, 0, 1f);
            set.levelUpBurst = SavePrefab(lu, "LevelUpBurst");

            // Continuous trails/sparkles attached to entities.
            var tr = NewPS("MeteorTrail", null, m.vfxGlow, false, 9);
            main = tr.main; main.loop = true; main.playOnAwake = true; main.duration = 1f; main.startLifetime = 0.38f; main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.7f); main.startColor = Fire; main.maxParticles = 80;
            var trem = tr.emission; trem.rateOverTime = 4; trem.rateOverDistance = 22;
            var trshape = tr.shape; trshape.radius = 0.12f;
            SizeLife(tr, 1f, 0f); ColorLife(tr, Hot, Ember, 0.7f);
            var trs = NewPS("Smoke", tr.transform, m.vfxSmoke, false, 8);
            main = trs.main; main.loop = true; main.playOnAwake = true; main.duration = 1f; main.startLifetime = 0.75f; main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); main.startColor = new Color(0.3f, 0.26f, 0.24f, 0.4f); main.maxParticles = 60;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var trsem = trs.emission; trsem.rateOverDistance = 7;
            var trsshape = trs.shape; trsshape.radius = 0.15f;
            SizeLife(trs, 0.6f, 1.6f); ColorLife(trs, new Color(0.35f, 0.3f, 0.28f), new Color(0.1f, 0.1f, 0.12f), 0.5f);
            set.meteorTrail = SavePrefab(tr, "MeteorTrail");

            var sp = NewPS("GoldSparkle", null, m.vfxStar, false, 6);
            main = sp.main; main.loop = true; main.playOnAwake = true; main.duration = 1f; main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f); main.startColor = Palette.GoldLight;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var spem = sp.emission; spem.rateOverTime = 7;
            var spshape = sp.shape; spshape.radius = 0.45f;
            SizeLife(sp, 0f, 0f, 1f); ColorLife(sp, Color.white, Palette.Gold);
            set.goldSparkle = SavePrefab(sp, "GoldSparkle");

            var db = NewPS("Debris", null, m.vfxDebris, false, 19);
            main = db.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.26f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.32f, 0.26f), new Color(0.25f, 0.2f, 0.18f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var dbrot = db.rotationOverLifetime; dbrot.enabled = true; dbrot.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            var dbshape = db.shape; dbshape.radius = 0.25f;
            Burst(db, 7, 11); ColorLife(db, Color.white, new Color(0.6f, 0.6f, 0.6f), 1f); SizeLife(db, 1, 0.4f); Drag(db, 0.1f);
            set.debris = SavePrefab(db, "Debris");
            return set;
        }

        // ------------------------------------------------------------------ audio

        static AudioClip Clip(string rel)
        {
            string path = $"{Audio}/{rel}.ogg";
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) throw new FileNotFoundException("Missing audio " + path);
            bool music = rel.StartsWith("OGA_DarkSciFi/") && !rel.EndsWith("victory");
            imp.forceToMono = !music;
            imp.loadInBackground = music;
            var s = imp.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.45f : 0.7f;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static AudioLibrary.Cue Cue(Sfx id, float volume, params string[] clips)
        {
            var list = new List<AudioClip>();
            foreach (var c in clips) list.Add(Clip(c));
            return new AudioLibrary.Cue { id = id, clips = list.ToArray(), volume = volume, pitchJitter = 0.06f, minGap = id == Sfx.Shot ? 0.05f : 0.03f };
        }

        static AudioLibrary BuildAudio()
        {
            const string path = Root + "/Audio/AudioLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(path);
            if (lib == null) { lib = ScriptableObject.CreateInstance<AudioLibrary>(); AssetDatabase.CreateAsset(lib, path); }
            lib.cues = new[]
            {
                Cue(Sfx.Shot, 0.55f, "Kenney_SciFi/laserSmall_000", "Kenney_SciFi/laserSmall_001"),
                Cue(Sfx.DroneShot, 0.6f, "Kenney_SciFi/laserRetro_000"),
                Cue(Sfx.Laser, 0.9f, "Kenney_SciFi/laserLarge_002"),
                Cue(Sfx.MeteorHit, 0.7f, "Kenney_Impact/impactMetal_light_001", "Kenney_Impact/impactMetal_light_002"),
                Cue(Sfx.Explosion, 0.8f, "Kenney_SciFi/explosionCrunch_000", "Kenney_SciFi/explosionCrunch_002", "Kenney_SciFi/explosionCrunch_003"),
                Cue(Sfx.BigExplosion, 1f, "Kenney_SciFi/lowFrequency_explosion_000"),
                Cue(Sfx.PlanetHit, 1f, "Kenney_Impact/impactPunch_medium_000", "Kenney_Impact/impactPunch_medium_001"),
                Cue(Sfx.ShieldBlock, 0.9f, "Kenney_Impact/impactMetal_medium_000", "Kenney_SciFi/forceField_000"),
                Cue(Sfx.GoldPickup, 0.9f, "Kenney_Interface/glass_002", "Kenney_Interface/glass_003"),
                Cue(Sfx.GoldShatter, 0.8f, "Kenney_Impact/impactGlass_light_000", "Kenney_Impact/impactGlass_light_001"),
                Cue(Sfx.LevelUp, 0.9f, "Kenney_Interface/maximize_004"),
                Cue(Sfx.Purchase, 0.9f, "Kenney_Interface/maximize_006", "Kenney_Interface/confirmation_001"),
                Cue(Sfx.Denied, 0.7f, "Kenney_Interface/error_001"),
                Cue(Sfx.UiClick, 0.6f, "Kenney_Interface/click_001", "Kenney_Interface/select_007"),
                Cue(Sfx.Unlock, 1f, "Kenney_Interface/bong_001", "Kenney_Interface/maximize_005"),
                Cue(Sfx.Countdown, 0.8f, "Kenney_Interface/drop_002"),
                Cue(Sfx.Freeze, 0.8f, "Kenney_SciFi/forceField_003"),
                Cue(Sfx.Revive, 0.9f, "Kenney_SciFi/forceField_001"),
                Cue(Sfx.GameOver, 1f, "Kenney_SciFi/lowFrequency_explosion_001"),
                Cue(Sfx.NewBest, 0.9f, "OGA_DarkSciFi/victory", "Kenney_Interface/confirmation_003"),
            };
            lib.menuMusic = Clip("OGA_DarkSciFi/airy");
            lib.combatMusic = Clip("OGA_DarkSciFi/sector");
            lib.pressureMusic = Clip("OGA_DarkSciFi/urgent");
            lib.musicVolume = 0.45f;
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------------ post-processing

        static VolumeProfile BuildVolume()
        {
            Directory.CreateDirectory(Settings);
            string path = Settings + "/PD_PostFX.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.85f); bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(false);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f); vignette.smoothness.Override(0.45f); vignette.color.Override(new Color(0.01f, 0.01f, 0.04f));
            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ------------------------------------------------------------------ assets + scene

        static GameAssets BuildGameAssets(MaterialSet m, VfxSet v, AudioLibrary audio, VolumeProfile profile)
        {
            string dir = Root + "/Resources";
            Directory.CreateDirectory(dir);
            string path = dir + "/GameAssets.asset";
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<GameAssets>(); AssetDatabase.CreateAsset(a, path); }
            a.planet = m.planet; a.planetRing = m.ring; a.atmosphereGlow = m.glow; a.meteor = m.meteor; a.gold = m.gold; a.bolt = m.bolt;
            a.glow = m.glow; a.shield = m.shield; a.laser = m.laser; a.starfield = m.starfield; a.reticle = m.reticle;
            a.quad = S("quad"); a.softCircle = S("soft_circle"); a.ring = S("ring"); a.star4 = S("star4"); a.spark = S("spark");
            a.bolt2D = S("bolt"); a.turret = S("turret"); a.crosshair = S("crosshair");
            a.panel = S("panel"); a.panelOutline = S("panel_outline"); a.pill = S("pill"); a.pillOutline = S("pill_outline"); a.bar = S("pill");
            a.heart = S("heart"); a.heartEmpty = S("heart_empty"); a.coin = S("coin"); a.pause = S("pause"); a.gear = S("gear"); a.lockIcon = S("lock");
            a.arrowLeft = S("arrow_left"); a.arrowRight = S("arrow_right"); a.musicIcon = S("music"); a.soundIcon = S("sound"); a.trophy = S("trophy");
            a.playIcon = S("play"); a.shieldIcon = S("shield_icon");
            var flags = new List<Sprite>();
            foreach (var code in Loc.Languages) flags.Add(S("flag_" + code));
            a.flags = flags.ToArray();
            var config = JsonUtility.FromJson<GameConfig>(File.ReadAllText(Root + "/Resources/Config/game_config.json"));
            var icons = new List<Sprite>();
            foreach (var up in config.upgrades) icons.Add(S("up_" + up.id));
            a.upgradeIcons = icons.ToArray();
            var maps = new List<Texture2D>();
            foreach (var p in config.planets)
            {
                string file = File.Exists($"{Planets}/{p.texture}.jpg") ? $"{Planets}/{p.texture}.jpg" : $"{Planets}/{p.texture}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                if (tex == null) throw new FileNotFoundException("Planet map missing: " + file);
                maps.Add(tex);
            }
            a.planetMaps = maps.ToArray();
            a.explosionSmall = v.explosionSmall; a.explosionBig = v.explosionBig; a.hitSpark = v.hitSpark; a.goldPickup = v.goldPickup;
            a.goldShatter = v.goldShatter; a.planetHit = v.planetHit; a.shockwave = v.shockwave; a.muzzle = v.muzzle; a.freezePulse = v.freezePulse;
            a.shieldBreak = v.shieldBreak; a.levelUpBurst = v.levelUpBurst; a.meteorTrail = v.meteorTrail; a.goldSparkle = v.goldSparkle; a.debris = v.debris;
            a.audio = audio;
            a.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            a.volumeProfile = profile;
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        static void BuildScene(GameAssets assets, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 40f;
            cam.allowHDR = true; cam.allowMSAA = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();

            var volGo = new GameObject("PostFX");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<GameManager>();
            var so = new SerializedObject(game);
            so.FindProperty("assets").objectReferenceValue = assets;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
