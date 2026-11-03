using UnityEngine;

namespace PlanetDefender
{
    // Gold: a faceted four-point star (PD/Gold). Spins and bobs; it never takes a rock texture or a trail so
    // it reads as income even in grayscale (GDD §13).
    public sealed class GoldPickup : MonoBehaviour
    {
        public int value;
        public float radius, speed, seed;
        public Vector2 position, direction;
        public bool alive;
        public SpriteRenderer body;
        public SpriteRenderer halo;
        public ParticleSystem sparkle;
        public Transform visual;

        public void Apply(float time)
        {
            transform.position = position;
            visual.localRotation = Quaternion.Euler(0, 0, time * 90f + seed * 360f);
            float pulse = 1f + Mathf.Sin(time * 6f + seed * 10f) * 0.06f;
            visual.localScale = Vector3.one * pulse;
            body.color = new Color(seed, 0, 0, 1);
        }
    }
}
