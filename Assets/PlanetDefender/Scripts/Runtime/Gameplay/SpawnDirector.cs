using UnityEngine;

namespace PlanetDefender
{
    // Difficulty ramp (GDD §9): a calm tutorial window, then ~7% more density every 30 s within floor/ceiling
    // limits, new meteor types joining the pool over time, and gold every 6–9 s with a hard cap on dry spells.
    public sealed class SpawnDirector
    {
        readonly GameConfig config;
        readonly System.Random rng;
        float meteorTimer, goldTimer, sinceGold;

        public SpawnDirector(GameConfig config, int seed)
        {
            this.config = config;
            rng = new System.Random(seed);
            Reset();
        }

        public void Reset()
        {
            meteorTimer = 1.2f;
            goldTimer = NextGoldInterval() * 0.6f;   // the first gold arrives early so the rule is learned in the opening
            sinceGold = 0;
        }

        public float MeteorInterval(float elapsed)
        {
            var s = config.spawn;
            if (elapsed < s.tutorialSeconds) return s.startInterval;
            float steps = (elapsed - s.tutorialSeconds) / s.rampEvery;
            return Mathf.Max(s.minInterval, s.startInterval / Mathf.Pow(s.rampFactor, steps));
        }

        public float SpeedMultiplier(float elapsed)
        {
            var s = config.spawn;
            return Mathf.Min(s.maxSpeedMultiplier, 1f + s.speedRampPerStep * (elapsed / s.rampEvery));
        }

        // Returns how many meteors to spawn this frame (0, 1 or a small burst late in the run).
        public int TickMeteors(float dt, float elapsed, int alive, int cap)
        {
            meteorTimer -= dt;
            if (meteorTimer > 0) return 0;
            float interval = MeteorInterval(elapsed);
            meteorTimer += interval * (0.75f + (float)rng.NextDouble() * 0.5f);
            if (alive >= cap) return 0;
            int count = 1;
            if (elapsed >= config.spawn.burstFrom && rng.NextDouble() < config.spawn.burstChance) count = 2 + (rng.NextDouble() < 0.3 ? 1 : 0);
            return Mathf.Min(count, cap - alive);
        }

        public bool TickGold(float dt)
        {
            goldTimer -= dt;
            sinceGold += dt;
            if (goldTimer > 0 && sinceGold < config.gold.pityMax) return false;
            goldTimer = NextGoldInterval();
            sinceGold = 0;
            return true;
        }

        float NextGoldInterval() => Mathf.Lerp(config.gold.minInterval, config.gold.maxInterval, (float)rng.NextDouble());

        public MeteorConfig PickMeteor(float elapsed)
        {
            float total = 0;
            foreach (var m in config.meteors) if (m.unlockAt >= 0 && elapsed >= m.unlockAt) total += m.weight;
            float roll = (float)rng.NextDouble() * total;
            foreach (var m in config.meteors)
            {
                if (m.unlockAt < 0 || elapsed < m.unlockAt) continue;
                roll -= m.weight;
                if (roll <= 0) return m;
            }
            return config.meteors[0];
        }

        public GoldVariant PickGold()
        {
            float total = 0;
            foreach (var g in config.gold.variants) total += g.weight;
            float roll = (float)rng.NextDouble() * total;
            foreach (var g in config.gold.variants) { roll -= g.weight; if (roll <= 0) return g; }
            return config.gold.variants[0];
        }

        // A point just outside the visible rectangle, uniform along its perimeter.
        public Vector2 EdgePoint(Rect view, float margin)
        {
            var r = new Rect(view.xMin - margin, view.yMin - margin, view.width + margin * 2, view.height + margin * 2);
            float perimeter = 2 * (r.width + r.height);
            float t = (float)rng.NextDouble() * perimeter;
            if (t < r.width) return new Vector2(r.xMin + t, r.yMax);
            t -= r.width;
            if (t < r.height) return new Vector2(r.xMax, r.yMax - t);
            t -= r.height;
            if (t < r.width) return new Vector2(r.xMax - t, r.yMin);
            t -= r.width;
            return new Vector2(r.xMin, r.yMin + t);
        }

        public float Range(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
    }
}
