using DG.Tweening;
using UnityEngine;

namespace PlanetDefender
{
    // Meteor body: one quad drawn by PD/Meteor. Per-instance data rides in the vertex colour so every meteor
    // batches: r = shape seed, g = type, b = hit flash, a = damage (glowing cracks).
    public sealed class Meteor : MonoBehaviour
    {
        public MeteorConfig config;
        public int typeIndex;
        public float hp, maxHp, radius, speed, seed, flash, spawnTime;
        public Vector2 position, direction;
        public bool alive;
        public SpriteRenderer body;
        public ParticleSystem trail;
        public Transform visual;
        public int id;
        public bool frozenTint;

        public void Apply()
        {
            transform.position = position;
            float damage = maxHp > 1 ? 1f - hp / maxHp : 0f;
            body.color = new Color(seed, typeIndex / 8f, Mathf.Clamp01(flash), damage);
        }

        public void Hit()
        {
            flash = 1f;
            visual.DOKill(true);
            visual.localScale = Vector3.one;
            visual.DOPunchScale(Vector3.one * 0.22f, 0.18f, 8, 0.6f);
        }
    }
}
