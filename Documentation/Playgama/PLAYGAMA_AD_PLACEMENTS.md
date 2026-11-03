# Planet Defender — Playgama ad placements

All ads go through Playgama Bridge (`com.playgama.bridge` v2.4.0). The project has no other ad SDK. (Unity added Unity
Ads and IAP to the manifest on first import; they were removed.) Placement ids are declared in
`Assets/WebGLTemplates/Bridge/playgama-bridge-config.json`.

| Placement id | Type | Trigger (real game event) | Code |
|---|---|---|---|
| `level_up` | Interstitial | The XP bar fills: the game freezes, the request goes out, then the upgrade cards open | `GameManager.BeginLevelUp` |
| `game_over` | Interstitial | Result screen → **RETRY** or **PLANETS** | `GameManager.Retry` / `GameOverToShop` → `AfterBreak` |
| `main_menu` | Interstitial | Result screen → back arrow (main menu) | `GameManager.GameOverToMenu` |
| `revive` | Rewarded | Last life lost → **WATCH AD: +1 LIFE** (once per run) | `GameManager.WatchRevive` |
| — | Banner | Not used | — |

## Rules applied (Playgama 2.1–2.2, GDD §10)
- **Only at breaks (2.1.3).** Level-up is a full stop (time scale 0), and the result screen is after the run. There is no ad during live play, and none on PLAY from the menu.
- **No frequency logic of our own.** Every natural break calls `ShowInterstitial`; Bridge decides whether an ad shows (its default minimum interval is 60 s). The GDD's "about every 3 levels" is therefore a target, not a guarantee.
- **Pause during ads (2.1.6).** On `Opened`, `AudioListener.pause` is set and gameplay stays frozen. On `Closed`/`Failed`, audio is restored and the flow continues.
- **Never blocks (2.1.7).** Unsupported → continue immediately. No state within 0.6 s (Bridge skipped the request) or still loading after 8 s → continue.
- **Rewarded is opt-in and honest (2.2.1–2.2.2).** The button says it shows an ad and that the reward is +1 life. **END RUN** next to it is always free. The offer is only shown when `isRewardedSupported` is true.
- **Once per attempt (2.2.4).** `RunState.reviveUsed` is set when the revive is granted, so the offer never returns in the same run. Normal life losses never offer an ad.
- **Reward only on `Rewarded` (2.2.5).** +1 HP, 2 s invulnerability and a 3-2-1 countdown, keeping score, XP, gold and upgrades. `Closed`/`Failed` without `Rewarded` leads to the normal game-over screen.

## Analytics (Bridge only)
`session_start`, `run_start`, `level_up`, `upgrade_buy`, `planet_buy`, `interstitial_request`, `interstitial_shown`,
`rewarded_offer`, `rewarded_success`, `run_end`, `leaderboard_submit`.
