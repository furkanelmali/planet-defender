# Planet Defender — Playgama ad placements

All ads go through Playgama Bridge (`com.playgama.bridge` v2.4.0). The project has no other ad SDK. (Unity added Unity
Ads and IAP to the manifest on first import; they were removed.) Placement ids are declared in
`Assets/WebGLTemplates/Bridge/playgama-bridge-config.json`.

| Placement id | Type | Trigger (real game event) | Code |
|---|---|---|---|
| `level_up` | Interstitial | The player leaves the upgrade cards (buy or Skip); after an ad the run resumes with a 3-2-1 countdown | `GameManager.CloseLevelUp` |
| `game_over` | Interstitial | Result screen → **RETRY**, or the next **PLAY** after going to **PLANETS** from the result screen | `GameManager.Retry` / `Play` → `AfterBreak` |
| `main_menu` | Interstitial | Result screen → back arrow (main menu) | `GameManager.GameOverToMenu` |
| `revive` | Rewarded | Last life lost → **WATCH AD: +1 LIFE** (once per run) | `GameManager.WatchRevive` |
| `double_gold` | Rewarded | Result screen → **WATCH AD: x2 GOLD** (once per run, at least 20 gold banked) | `GameManager.WatchDoubleGold` |
| `upgrade_free` | Rewarded | Level-up → one card the player cannot afford shows **AD: FREE** (once per level-up) | `GameManager.WatchUpgradeAd` |
| `daily_double` | Rewarded | Daily reward → **WATCH AD: x2** | `GameManager.WatchDailyDouble` |
| — | Banner | Not used | — |

## Rules applied (Playgama 2.1–2.2, GDD §10)
- **Only at breaks (2.1.3).** Cards, results and the menu are full stops. There is no ad during live play.
- **Our own pacing, on top of the platform's.** No interstitial in the first 45 s after play starts in a session, then at least 60 s since the last ad of any kind (`ads` in `game_config.json`). On Playgama, Bridge's own 60 s delay can be switched off when the portal manages frequency, so it is not relied on.
- **Rewarded ads are not limited by pacing,** but they count as the last ad: an opt-in ad is not followed straight away by a forced one.
- **Pause during ads (2.1.6).** A permanent listener freezes and silences the game on `Opened`, including an ad that opens after its request timed out.
- **Never blocks (2.1.7).** Unsupported → continue immediately. No state within 0.6 s or still loading after 8 s → continue.
- **Rewarded is opt-in and honest (2.2.1–2.2.2).** Every button says it shows an ad and what it pays. A free path always sits next to it.
- **Reward only on `Rewarded` (2.2.5).** `Closed`/`Failed` without `Rewarded` pays nothing; the player sees a message and can try again or take the free path.

## Analytics (Bridge only)
Session and first minute: `session_start` (`new_player`, `mobile`), `first_input`, `first_kill`, `first_gold`, `first_level_up`.
Run: `run_start`, `level_up`, `upgrade_buy`, `sector_reached`, `mission_complete`, `run_end` (`sector`, `first_run`, `missions`, `mobile`).
Economy: `planet_buy`, `daily_claim`, `leaderboard_submit`.
Ads: `interstitial_request`, `interstitial_shown`, `rewarded_offer`, `rewarded_offer_taken`, `rewarded_success`, `rewarded_failed`, `revive_decline` (rewarded events carry `placement`).
