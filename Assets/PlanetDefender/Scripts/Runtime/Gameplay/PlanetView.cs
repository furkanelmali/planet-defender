using DG.Tweening;
using UnityEngine;

namespace PlanetDefender
{
    // The defended planet: a real surface map spun on a lit sphere (PD/Planet), Saturn's ring split into a back
    // and a front half, a soft atmosphere halo, the blaster turret and the shield bubble.
    public sealed class PlanetView : MonoBehaviour
    {
        // PD/Planet draws the sphere inside 80% of its quad; the outer band is the atmosphere rim.
        public const float SphereFill = 0.8f;
        static readonly int MainTex = Shader.PropertyToID("_SurfaceMap"), Atmo = Shader.PropertyToID("_AtmoColor"),
            Spin = Shader.PropertyToID("_Spin"), Flash = Shader.PropertyToID("_Flash"), Lod = Shader.PropertyToID("_Lod"),
            FlashColor = Shader.PropertyToID("_FlashColor"), RingFront = Shader.PropertyToID("_Front");

        Transform pivot, turret, barrel;
        SpriteRenderer body, halo, ringBack, ringFront, shield;
        Material bodyMaterial;
        Tween flashTween, shieldTween;
        float radius;
        public PlanetConfig Config { get; private set; }
        public float Radius => radius;

        public static PlanetView Create(GameAssets assets, Transform parent)
        {
            var go = new GameObject("Planet");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PlanetView>();
            view.Build(assets);
            return view;
        }

        void Build(GameAssets assets)
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            halo = Sprite(pivot, "Halo", assets.softCircle, assets.glow, -3);
            ringBack = Sprite(pivot, "RingBack", assets.quad, assets.planetRing, -2);
            bodyMaterial = new Material(assets.planet);
            body = Sprite(pivot, "Body", assets.quad, bodyMaterial, 0);
            ringFront = Sprite(pivot, "RingFront", assets.quad, new Material(assets.planetRing), 1);
            ringBack.sharedMaterial = new Material(assets.planetRing);
            ringBack.sharedMaterial.SetFloat(RingFront, 0f);
            ringFront.sharedMaterial.SetFloat(RingFront, 1f);
            shield = Sprite(pivot, "Shield", assets.quad, new Material(assets.shield), 3);
            shield.gameObject.SetActive(false);

            turret = new GameObject("Turret").transform;
            turret.SetParent(transform, false);
            barrel = Sprite(turret, "Barrel", assets.turret, null, 2).transform;
            barrel.GetComponent<SpriteRenderer>().color = Palette.PlasmaSoft;
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        public void Setup(PlanetConfig config, Texture2D map, float collisionRadius, bool showTurret = true)
        {
            Config = config;
            radius = collisionRadius * config.visualScale;
            float quad = radius * 2f / SphereFill;
            body.transform.localScale = Vector3.one * quad;
            bodyMaterial.SetTexture(MainTex, map);
            bodyMaterial.SetColor(Atmo, config.AtmosphereColor);
            bodyMaterial.SetFloat(Spin, config.spin);
            bodyMaterial.SetFloat(Flash, 0);
            halo.transform.localScale = Vector3.one * radius * 4.2f;
            halo.color = config.AtmosphereColor.WithAlpha(0.2f);
            bool ring = config.ring > 0;
            ringBack.gameObject.SetActive(ring); ringFront.gameObject.SetActive(ring);
            ringBack.transform.localScale = ringFront.transform.localScale = Vector3.one * radius * 4.6f;
            ringBack.transform.localRotation = ringFront.transform.localRotation = Quaternion.Euler(0, 0, -14f);
            shield.transform.localScale = Vector3.one * radius * 2.9f;
            turret.gameObject.SetActive(showTurret);
            barrel.localPosition = new Vector3(0, radius * 0.98f, 0);
            barrel.localScale = Vector3.one * 0.55f;
        }

        // Texture LOD from the on-screen size keeps the equirect seam and far zoom free of shimmer.
        public void UpdateLod(Camera camera)
        {
            if (camera == null) return;
            float pixels = radius * 2f / (camera.orthographicSize * 2f) * Screen.height;
            var tex = bodyMaterial.GetTexture(MainTex);
            float width = tex != null ? tex.width : 1024;
            bodyMaterial.SetFloat(Lod, Mathf.Max(0, Mathf.Log(width / Mathf.Max(32f, pixels * 3.14f), 2f)));
        }

        public void Aim(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            turret.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public Vector2 Muzzle(Vector2 direction) => (Vector2)transform.position + direction.normalized * (radius * 1.12f);

        public void Recoil()
        {
            barrel.DOKill(true);
            barrel.DOPunchPosition(new Vector3(0, -0.12f, 0), 0.12f, 6, 0.5f);
        }

        public void Hit(Color color)
        {
            flashTween?.Kill();
            bodyMaterial.SetColor(FlashColor, color);
            bodyMaterial.SetFloat(Flash, 1);
            flashTween = DOTween.To(() => bodyMaterial.GetFloat(Flash), v => bodyMaterial.SetFloat(Flash, v), 0f, 0.45f);
            pivot.DOKill(true);
            pivot.localScale = Vector3.one;
            pivot.DOPunchScale(new Vector3(-0.14f, -0.14f, 0), 0.35f, 7, 0.7f);
        }

        public void Pulse(float amount = 0.08f)
        {
            pivot.DOKill(true);
            pivot.localScale = Vector3.one;
            pivot.DOPunchScale(Vector3.one * amount, 0.3f, 6, 0.6f);
        }

        public void SetShield(bool on, Color color, bool animate = true)
        {
            shieldTween?.Kill();
            var t = shield.transform;
            if (on)
            {
                shield.gameObject.SetActive(true);
                shield.color = color;
                if (!animate) { t.localScale = Vector3.one * radius * 2.9f; return; }
                t.localScale = Vector3.one * radius * 1.6f;
                shieldTween = t.DOScale(radius * 2.9f, 0.45f).SetEase(Ease.OutBack);
            }
            else if (shield.gameObject.activeSelf)
            {
                if (!animate) { shield.gameObject.SetActive(false); return; }
                shieldTween = t.DOScale(radius * 3.6f, 0.25f).SetEase(Ease.OutQuad).OnComplete(() => shield.gameObject.SetActive(false));
            }
        }

        public bool ShieldVisible => shield.gameObject.activeSelf;

        void OnDestroy()
        {
            flashTween?.Kill(); shieldTween?.Kill();
            if (bodyMaterial) Destroy(bodyMaterial);
        }
    }
}
