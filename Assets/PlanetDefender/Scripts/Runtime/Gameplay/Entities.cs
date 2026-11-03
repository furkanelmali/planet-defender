using UnityEngine;

namespace PlanetDefender
{
    public sealed class Bolt
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 position, velocity;
        public float damage, radius;
        public int pierceLeft;
        public bool meteorOnly, alive;
        public int lastHitId = -1, lastHitId2 = -1;
    }

    public sealed class Drone
    {
        public Transform transform;
        public float angle, cooldown;
    }
}
