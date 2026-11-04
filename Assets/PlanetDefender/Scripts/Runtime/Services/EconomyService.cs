using System.Collections.Generic;

namespace PlanetDefender
{
    public struct Settlement
    {
        public bool applied;          // false when this run was already settled (reload, double tap)
        public int banked, bankBefore, bankAfter;
        public bool newBest;
        public int best;
    }

    // Two wallets (GDD §5): Session Gold lives in the run, Bank Gold in the save. Only settlement moves gold
    // from one to the other, once per runId.
    public static class EconomyService
    {
        public static int BankGold => SaveService.Data.bankGold;

        public static bool IsUnlocked(string planet) => SaveService.Data.unlockedPlanets.Contains(planet);

        public static bool CanBuy(PlanetConfig planet) => !IsUnlocked(planet.id) && BankGold >= planet.price;

        // Planets are bought once with Bank Gold, kept forever, never sold (GDD §4).
        public static bool BuyPlanet(PlanetConfig planet)
        {
            if (!CanBuy(planet)) return false;
            var data = SaveService.Data;
            data.bankGold -= planet.price;
            data.unlockedPlanets.Add(planet.id);
            data.selectedPlanet = planet.id;
            SaveService.Save();
            PlaygamaBridgeAdapter.Analytics("planet_buy", new Dictionary<string, object> { { "planet", planet.id }, { "price", planet.price } });
            return true;
        }

        public static void SelectPlanet(string planet)
        {
            if (!IsUnlocked(planet) || SaveService.Data.selectedPlanet == planet) return;
            SaveService.Data.selectedPlanet = planet;
            SaveService.Save();
        }

        // Rewards earned outside a run (daily reward, doubled gold) go straight to the bank.
        public static void AddBank(int amount)
        {
            if (amount <= 0) return;
            SaveService.Data.bankGold += amount;
            SaveService.Save();
        }

        // The goal the economy points at: the cheapest planet the player does not own yet (null when all are owned).
        public static PlanetConfig NextPlanet(GameConfig config)
        {
            PlanetConfig next = null;
            foreach (var p in config.planets)
                if (!IsUnlocked(p.id) && (next == null || p.price < next.price)) next = p;
            return next;
        }

        // runSettled false → true happens exactly once: lastSettledRunId and the new bank balance share one write.
        public static Settlement Settle(string runId, int unspentSessionGold, int sessionScore)
        {
            var data = SaveService.Data;
            var result = new Settlement { bankBefore = data.bankGold, bankAfter = data.bankGold, best = data.bestScore };
            if (string.IsNullOrEmpty(runId) || data.lastSettledRunId == runId) return result;
            int banked = unspentSessionGold < 0 ? 0 : unspentSessionGold;
            data.bankGold += banked;
            data.lastSettledRunId = runId;
            if (sessionScore > data.bestScore)
            {
                data.bestScore = sessionScore;
                data.bestScorePending = true;
                result.newBest = true;
            }
            data.hasSnapshot = false;
            data.runSnapshot = new RunSnapshot();
            SaveService.Save();
            result.applied = true;
            result.banked = banked;
            result.bankAfter = data.bankGold;
            result.best = data.bestScore;
            return result;
        }
    }
}
