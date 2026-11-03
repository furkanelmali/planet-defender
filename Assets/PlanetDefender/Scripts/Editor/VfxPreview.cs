using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PlanetDefender.EditorTools
{
    // Renders every particle prefab (mid-burst) plus the planet in a hit flash onto one contact sheet, so VFX can be
    // reviewed without playing. Batch: -executeMethod PlanetDefender.EditorTools.VfxPreview.Run  → Logs/vfx_sheet.png
    public static class VfxPreview
    {
        [MenuItem("Planet Defender/Render VFX Sheet")]
        public static void Render()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/PlanetDefender/Resources/GameAssets.asset");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Cam");
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 8; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.Void;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var vol = new GameObject("Vol").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = assets.volumeProfile;

            var prefabs = new[] { assets.explosionSmall, assets.explosionBig, assets.hitSpark, assets.goldPickup, assets.goldShatter, assets.planetHit,
                assets.shockwave, assets.muzzle, assets.freezePulse, assets.shieldBreak, assets.levelUpBurst, assets.debris };
            for (int i = 0; i < prefabs.Length; i++)
            {
                var ps = Object.Instantiate(prefabs[i]);
                ps.transform.position = new Vector3(-4.5f + (i % 3) * 4.5f, 5.5f - (i / 3) * 4f, 0);
                ps.Simulate(0.08f, true, true);
            }
            var planetGo = new GameObject("Planet");
            var sr = planetGo.AddComponent<SpriteRenderer>();
            sr.sprite = assets.quad;
            var mat = new Material(assets.planet);
            mat.SetTexture("_SurfaceMap", assets.planetMaps[0]);
            mat.SetFloat("_Flash", 1f);
            mat.SetColor("_FlashColor", Palette.Danger);
            sr.sharedMaterial = mat;
            planetGo.transform.position = new Vector3(0, -10.5f, 0);
            planetGo.transform.localScale = Vector3.one * 3.25f;
            cam.orthographicSize = 13; cam.transform.position = new Vector3(0, -1.5f, -10);

            var rt = new RenderTexture(900, 1300, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.GetFullPath("Logs/vfx_sheet.png"), img.EncodeToPNG());
            Debug.Log("PD_VFX_SHEET_OK");
        }

        public static void Run()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
