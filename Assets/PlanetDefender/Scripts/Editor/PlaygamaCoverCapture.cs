using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetDefender.EditorTools
{
    // Store covers rendered from the real game (no paint-overs): plays a run, stages meteors, bolts, an explosion and
    // gold around the planet, freezes the frame and renders it at the exact Playgama cover sizes with a key-art title.
    // Batch, with graphics, without -quit:  -executeMethod PlanetDefender.EditorTools.PlaygamaCoverCapture.Run
    // Output: Documentation/Playgama/Covers/
    [InitializeOnLoad]
    public static class PlaygamaCoverCapture
    {
        const string Key = "PD.Cover.Active";
        const string Output = "Documentation/Playgama/Covers";

        sealed class Slot
        {
            public string name; public int width, height;
            public Vector2 camera; public float ortho;
            public Vector2 title;               // title block centre in viewport (0..1)
            public float titleSize;             // font size in pixels
            public (string type, Vector2 at)[] meteors;
            public Vector2 gold, burst, aim;
        }

        static readonly Slot[] Slots =
        {
            new Slot
            {
                name = "cover_square_800x800", width = 800, height = 800, camera = new Vector2(0, 0.85f), ortho = 3.55f,
                title = new Vector2(0.5f, 0.84f), titleSize = 118,
                meteors = new[] { ("big", new Vector2(-2.35f, 1.55f)), ("fast", new Vector2(2.55f, 1.1f)), ("armored", new Vector2(2.45f, -1.75f)),
                    ("normal", new Vector2(-2.7f, -1.1f)), ("splitter", new Vector2(0.95f, 2.55f)) },
                gold = new Vector2(2.25f, 0.95f), burst = new Vector2(1.55f, 1.55f), aim = new Vector2(-0.75f, 1f),
            },
            new Slot
            {
                name = "cover_portrait_1080x1920", width = 1080, height = 1920, camera = new Vector2(0, 1.75f), ortho = 5.0f,
                title = new Vector2(0.5f, 0.855f), titleSize = 158,
                meteors = new[] { ("big", new Vector2(-1.35f, 3.2f)), ("fast", new Vector2(2.0f, 2.1f)), ("normal", new Vector2(1.35f, 4.3f)),
                    ("armored", new Vector2(-2.05f, 0.75f)), ("splitter", new Vector2(2.1f, -2.3f)), ("normal", new Vector2(-1.9f, -2.6f)) },
                gold = new Vector2(2.2f, 0.55f), burst = new Vector2(1.05f, 1.45f), aim = new Vector2(-0.45f, 1f),
            },
            new Slot
            {
                name = "cover_landscape_1920x1080", width = 1920, height = 1080, camera = new Vector2(-2.7f, 0.35f), ortho = 3.7f,
                title = new Vector2(0.285f, 0.6f), titleSize = 132,
                meteors = new[] { ("big", new Vector2(-2.3f, 2.2f)), ("fast", new Vector2(2.6f, 2.4f)), ("armored", new Vector2(2.9f, -1.6f)),
                    ("normal", new Vector2(1.2f, 2.95f)), ("splitter", new Vector2(-1.9f, -2.5f)) },
                gold = new Vector2(2.55f, 0.25f), burst = new Vector2(1.6f, 1.35f), aim = new Vector2(-0.8f, 1f),
            },
        };

        static int step, slotIndex;
        static double next;
        static float runTime;
        static GameManager game;
        static GameObject titleRoot;

        static PlaygamaCoverCapture() { EditorApplication.update += Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene(PlanetDefenderBuilder.ScenePath);
            PlayerPrefs.DeleteKey("bridge_storage_data_" + SaveService.Key);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void Wait(double s) => next = EditorApplication.timeSinceStartup + s;

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < next) return;
            // Staged kills must never open a level-up (it would freeze the run mid-composition).
            if (game != null && game.Run != null) { game.Run.xp = 0; game.Run.hp = game.Run.maxHp; }
            try { Step(); }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Step()
        {
            switch (step)
            {
                case 0:
                    game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                    if (game == null || game.State != GameState.Menu) { Wait(0.2); return; }
                    PlaygamaBridgeAdapter.SkipAdsForTests = true;
                    SaveService.Data.tutorialDone = true;
                    Directory.CreateDirectory(Output);
                    step++; Wait(0.5); return;
                case 1:
                {
                    var slot = Slots[slotIndex];
                    // A fresh run per cover keeps the field clean.
                    if (game.State != GameState.Menu) { game.UI.HideAll(true); game.EnterMenu(); Wait(0.5); return; }
                    game.Play();
                    game.UI.GetComponent<Canvas>().enabled = false;
                    var cam = game.Juice.Camera;
                    var old = cam.targetTexture;
                    cam.targetTexture = new RenderTexture(slot.width, slot.height, 24, RenderTextureFormat.ARGB32);
                    if (old != null) { old.Release(); UnityEngine.Object.DestroyImmediate(old); }
                    step++; Wait(0.6); return;
                }
                case 2:
                {
                    var slot = Slots[slotIndex];
                    var a = game.Arena;
                    a.ClearField();
                    // Meteors start ~1.1 units further out so their trails have formed when they reach their mark.
                    foreach (var (type, at) in slot.meteors)
                    {
                        Vector2 away = (at - Arena.PlanetCenter).normalized;
                        a.DebugSpawn(type, at + away * 1.1f);
                    }
                    a.DebugSpawnGold(slot.gold + (slot.gold - Arena.PlanetCenter).normalized * 1.25f);
                    a.DebugAim = slot.aim;
                    runTime = game.Run.elapsed;
                    step++; Wait(0.2); return;
                }
                case 3:
                {
                    var slot = Slots[slotIndex];
                    game.Run.hp = game.Run.maxHp;
                    if (game.Run.elapsed - runTime < 0.85f) { Wait(0.2); return; }
                    game.Vfx.Play(game.Assets.explosionSmall, slot.burst, 1.7f);
                    game.Vfx.Play(game.Assets.debris, slot.burst, 1.2f);
                    runTime = game.Run.elapsed;
                    step++; Wait(0.1); return;
                }
                case 4:
                {
                    var slot = Slots[slotIndex];
                    if (game.Run.elapsed - runTime < 0.07f) { Wait(0.05); return; }
                    TimeController.Freeze();
                    game.Arena.DebugAim = null;
                    game.Arena.DebugCalm();
                    // Frame: move the camera off the in-game framing for a key-art composition.
                    var cam = game.Juice.Camera;
                    cam.transform.position = new Vector3(slot.camera.x, slot.camera.y, cam.transform.position.z);
                    game.Juice.SetBaseSize(slot.ortho);
                    cam.orthographicSize = slot.ortho;
                    BuildTitle(slot, cam);
                    step++; Wait(1.2); return;
                }
                case 5:
                {
                    var slot = Slots[slotIndex];
                    var rt = game.Juice.Camera.targetTexture;
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    img.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(Output, slot.name + ".png"), img.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(img);
                    Debug.Log("PD_COVER " + slot.name);
                    UnityEngine.Object.DestroyImmediate(titleRoot);
                    game.Juice.Camera.transform.position = new Vector3(0, 0, game.Juice.Camera.transform.position.z);
                    TimeController.ResetAll();
                    game.UI.GetComponent<Canvas>().enabled = true;
                    slotIndex++;
                    if (slotIndex >= Slots.Length) { Debug.Log("PD_COVERS_OK"); Finish(0); return; }
                    step = 1; Wait(0.3); return;
                }
            }
        }

        // Key-art title: white/cyan gradient face, dark outline and a deep extruded shadow; bloom adds the glow.
        static void BuildTitle(Slot slot, Camera cam)
        {
            titleRoot = new GameObject("CoverTitle");
            var canvas = titleRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 2;
            canvas.sortingOrder = 300;
            var scaler = titleRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var font = game.Assets.font;

            var face = new Material(font.material);
            face.EnableKeyword("OUTLINE_ON");
            // Kept inside the font atlas padding: larger values bleed the glyph quads into visible boxes.
            face.SetFloat("_FaceDilate", 0.12f);
            face.SetFloat("_OutlineWidth", 0.2f);
            face.SetColor("_OutlineColor", new Color32(4, 8, 26, 255));
            var shadow = new Material(font.material);
            shadow.EnableKeyword("OUTLINE_ON");
            shadow.SetFloat("_FaceDilate", 0.12f);
            shadow.SetFloat("_OutlineWidth", 0.2f);
            shadow.SetColor("_OutlineColor", new Color32(4, 8, 26, 255));

            // TMP derives the SDF scale ratios from these properties; without this the outline fills whole glyph quads.
            ShaderUtilities.UpdateShaderRatios(face);
            ShaderUtilities.UpdateShaderRatios(shadow);

            float size = slot.titleSize;
            Vector2 centre = new Vector2((slot.title.x - 0.5f) * slot.width, (slot.title.y - 0.5f) * slot.height);
            Line("PLANET", new Vector2(centre.x, centre.y + size * 0.5f), size, Color.white, new Color(0.78f, 0.95f, 1f), face, shadow, slot);
            Line("DEFENDER", new Vector2(centre.x, centre.y - size * 0.5f), size * 1.0f, new Color(0.45f, 0.96f, 1f), new Color(0.12f, 0.62f, 1f), face, shadow, slot);
        }

        static void Line(string text, Vector2 position, float size, Color top, Color bottom, Material face, Material shadow, Slot slot)
        {
            float depth = size * 0.07f;
            for (int i = 3; i >= 1; i--)
            {
                var s = Text(text, position + new Vector2(depth * 0.35f * i / 3f, -depth * i / 3f), size, shadow, slot);
                s.color = Color.Lerp(new Color32(10, 40, 96, 255), new Color32(6, 16, 46, 255), i / 3f);
            }
            var t = Text(text, position, size, face, slot);
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        static TextMeshProUGUI Text(string text, Vector2 position, float size, Material material, Slot slot)
        {
            var go = new GameObject(text);
            go.transform.SetParent(titleRoot.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = game.Assets.font;
            t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.extraPadding = true;
            t.UpdateMeshPadding();
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slot.width * 1.2f, size * 1.4f);
            rt.anchoredPosition = position;
            return t;
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            PlaygamaBridgeAdapter.SkipAdsForTests = false;
            PlayerPrefs.DeleteKey("bridge_storage_data_" + SaveService.Key);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
