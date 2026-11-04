using System.Collections.Generic;
using UnityEngine;

namespace PlanetDefender
{
    public sealed class UpgradeOffer
    {
        public UpgradeConfig upgrade;
        public int index, currentLevel, price;
        public bool Affordable(RunState run) => run.sessionGold >= price;
    }

    // Level-up cards (GDD §7–8): three random upgrades that are not maxed, trying to include one the player can
    // afford. At most one purchase per level-up; Skip always works.
    public static class UpgradeShop
    {
        public static List<UpgradeOffer> Roll(GameConfig config, RunState run, System.Random rng, int count = 3, bool free = false)
        {
            var pool = new List<UpgradeOffer>();
            for (int i = 0; i < config.upgrades.Length; i++)
            {
                var up = config.upgrades[i];
                int level = run.Level(up.id);
                if (level >= up.maxLevel) continue;
                pool.Add(new UpgradeOffer { upgrade = up, index = i, currentLevel = level, price = config.UpgradePrice(up, level) });
            }
            for (int i = pool.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            var picked = pool.GetRange(0, Mathf.Min(count, pool.Count));
            bool anyAffordable = picked.Exists(o => o.Affordable(run));
            if (!anyAffordable && picked.Count > 0)
            {
                var cheap = pool.Find(o => o.Affordable(run) && !picked.Contains(o));
                if (cheap != null) picked[picked.Count - 1] = cheap;
            }
            picked.Sort((a, b) => a.price.CompareTo(b.price));
            if (free) foreach (var o in picked) o.price = 0;   // first-run gift: the first reward must never be out of reach
            return picked;
        }

        public static bool Buy(RunState run, UpgradeOffer offer)
        {
            if (offer == null || !offer.Affordable(run) || run.Level(offer.upgrade.id) != offer.currentLevel) return false;
            run.sessionGold -= offer.price;
            run.goldSpent += offer.price;
            run.upgrades[offer.upgrade.id] = offer.currentLevel + 1;
            PlaygamaBridgeAdapter.Analytics("upgrade_buy", new Dictionary<string, object>
                { { "upgrade", offer.upgrade.id }, { "tier", offer.currentLevel + 1 }, { "price", offer.price }, { "level", run.level } });
            return true;
        }
    }
}
