using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetDefender
{
    // One asset that the runtime reads everything from; generated and wired by the editor builder
    // (Planet Defender → Rebuild Assets), so nothing is looked up by string at runtime.
    [CreateAssetMenu(menuName = "Planet Defender/Game Assets")]
    public sealed class GameAssets : ScriptableObject
    {
        [Header("World materials")]
        public Material planet, planetRing, atmosphereGlow, meteor, gold, bolt, glow, shield, laser, starfield, reticle;

        [Header("Sprites")]
        public Sprite quad, softCircle, ring, star4, spark, bolt2D, turret, crosshair;

        [Header("UI sprites")]
        public Sprite panel, panelOutline, pill, pillOutline, bar, heart, heartEmpty, coin, pause, gear, lockIcon, arrowLeft, arrowRight,
            musicIcon, soundIcon, trophy, playIcon, shieldIcon;
        public Sprite[] upgradeIcons;   // same order as GameConfig.upgrades
        public Sprite[] flags;          // same order as Loc.Languages

        [Header("Planets (same order as GameConfig.planets)")]
        public Texture2D[] planetMaps;

        [Header("VFX prefabs")]
        public ParticleSystem explosionSmall, explosionBig, hitSpark, goldPickup, goldShatter, planetHit, shockwave, muzzle,
            freezePulse, shieldBreak, levelUpBurst, meteorTrail, goldSparkle, debris;

        [Header("Audio / Text / Post")]
        public AudioLibrary audio;
        public TMP_FontAsset font;
        public VolumeProfile volumeProfile;

        static GameAssets cached;
        public static GameAssets Load() => cached != null ? cached : cached = Resources.Load<GameAssets>("GameAssets");
    }
}
