# Planet Defender — Playgama WebGL release report

Branch `rebuild-unity6`. Date 2026-11-03. Unity 6000.4.8f1, URP 17.4, Playgama Bridge Unity **v2.4.0**, DOTween.
Sources checked: the Playgama wiki requirement pages (technical 1.x, advertising 2.x, UX 3.x, content 4.x, Unity 5.1),
the Bridge v2.4.0 source, and `Planet_Defender_GDD_v1.0`.

> **Verdict: technically ready for the console's Test Game step.** The SaaS public token is in the config; the
> `planet_defender_best_score` leaderboard must exist in the console. Real ads, the live leaderboard, phones and other browsers must
> still be checked in the console.

## A. Build output
| Item | Value |
|---|---|
| ZIP | `Builds/Playgama/PlanetDefender_Playgama_v1.0.zip`, **15,739,065 bytes (15.7 MB)**; limit 300 MB (1.3.1) |
| Build files (gzip) | wasm 8.2 MB, data 7.4 MB, framework 94 KB, loader 40 KB |
| Largest assets | 3 music loops (~2.8 MB), TMP Liberation Sans SDF + TTF (~1.3 MB), 8 planet maps (1024 px, ~2.7 MB) |
| Archive layout | `index.html` at the root, Latin file names only (1.3.2), checked by `PlaygamaWebBuild.Package` |

## B. Platform integration (GDD §10–11, §14)
- **Lifecycle:** the Bridge receiver is created before the first scene, then the platform language and audio state are read, Bridge Storage is loaded, and `game_ready` is sent once the first menu frame is on screen. Runs send `LevelStarted`/`LevelPaused`/`LevelResumed`/`LevelFailed`.
- **Storage:** one key, `pd_save_v1` (bank gold, planets, best score, settings, language, `lastSettledRunId`, run snapshot). It is written at purchases, level-up, pause and settlement — never per meteor or per gold. Settlement writes bank gold and the run id in one call (idempotent). There is no PlayerPrefs, localStorage or IndexedDB use on web, and data caching is off.
- **Ads:** see `PLAYGAMA_AD_PLACEMENTS.md`.
- **Leaderboard:** `planet_defender_best_score`. `setScore` runs only for a new best; a failed submit stays pending and is retried at boot and on the menu. `in_game` → list of the top 10 plus the player; `native_popup` → platform pop-up; `not_available`/`native` → personal best plus a note, never a fake list.
- **Pause/audio:** `pause_state_changed`, `audio_state_changed` and window focus all freeze live play and silence audio. `isAudioEnabled` is applied at start.
- **Analytics:** Bridge analytics only.

## C. Requirement compliance
| # | Requirement | Status |
|---|---|---|
| 1.1.1–1.1.2 | Current Bridge, required steps | PASS (v2.4.0, official template, config, language, storage, pause/audio, game_ready, ads) |
| 1.2.1–1.2.3 | No forced sign-in | PASS (no auth) |
| 1.2.4 | No external analytics | PASS |
| 1.2.5 | No external resources | PASS with note: only Bridge's own script from the official template |
| 1.2.6–1.2.7 | Links, URL locks | PASS (none) |
| 1.3.1–1.3.3 | Size, root `index.html`, < 2 GB at start | PASS (15.7 MB; 1 GB heap cap, not measured) |
| 1.4.1 | Full screen | PASS (canvas fills the page) |
| 1.4.3 | Orientation | ACTION: select **both** orientations in the draft (the layout adapts to both) |
| 1.4.4–1.4.5 | Touch controls, no long-press menu | Implemented (template guards); NOT TESTED on a phone |
| 1.4.6 | No engine warning on mobile | NOT TESTED on a device |
| 1.5.1–1.5.3 | Keyboard/mouse, **no Esc** | PASS (Esc removed this release; **P** pauses) |
| 1.6.1–1.6.8 | Adaptive, no scroll, no overlap, closable pop-ups, readable, 1:1 | PASS at 9:19.5, 9:16, 3:4, 16:9 (Expand canvas, corner HUD, modal cards) |
| 1.7.1–1.7.2 | No errors or freezes; playable through | PASS in play-mode checks (level-up Skip regression fixed); long sessions NOT TESTED |
| 1.7.3 | All major browsers | NOT TESTED beyond Chromium |
| 2.1.1–2.1.7 | Ads only via Bridge, at breaks, pause audio, never block | PASS (implementation + fallbacks); real ads NOT TESTED |
| 2.2.1–2.2.5 | Rewarded opt-in, honest, once per attempt, reward on completion only | PASS (revive at most once per run) |
| 2.3 / 2.4 / 2.5 | Banners / payments / real money | N/A (none); gold can never be cashed out |
| 3.1.1–3.1.3 | Progress, rising difficulty, onboarding | PASS (planets + best score, difficulty ramp, in-game tutorial hints) |
| 3.2.1–3.2.3 | Bridge storage only; best score saved; survives reload/rotation/ad | PASS by design; reload in browser NOT TESTED |
| 3.3.1–3.3.3 | Music + SFX, recorded audio, mute | PASS (CC0 recorded OGG, see `CREDITS.md`; settings toggles) |
| 3.3.4–3.3.5 | Platform audio state; silent when hidden | Implemented; NOT TESTED on the platform |
| 3.4.1 | English default and fallback | PASS |
| 3.4.2 | Language only from `platform.language` | PASS (fixed this release: start language = player's saved pick, else `platform.language` if supported, else English; never the OS or browser) |
| 3.4.3 | All text translated into declared languages | PASS (EN, TR, RU, ES: all 95 text keys translated; the 2 logo keys stay "PLANET DEFENDER" as the brand name) |
| 3.4.4 | Findable language selector | PASS (flag button on the main menu + gear → Settings → Language; native names + flags) |
| 3.5.1 | Title matches | PASS ("Planet Defender") |
| 4.1.x | Restricted content | PASS (abstract space shooter, no people or violence) |
| 4.2.1–4.2.3 | Rights, originality | CHECK: planet maps appear to be Solar System Scope (CC BY 4.0) — confirm the source and keep the attribution (`CREDITS.md`). Audio is CC0. Code and UI are original. |
| 4.3.1–4.3.2 | Not fully AI-generated; custom UI | PASS (procedural shaders, game-specific UI and icons, no emoji) |
| 5.1.1 | Decompression fallback | PASS |

## D. Changes in this release cycle
- Fixed: level-up screen sometimes stayed open after Skip (DOTween recycling + kept tween references).
- Fixed: main-menu buttons drifting on every visit (Play ending up under Planets/Ranking) and the level-up Skip button
  sometimes missing on phones. Interrupted entrance animations had become the next entrance's target; entrances are now
  keyed separately and always land on the layout position.
- Fixed: empty settings buttons; overlapping UI on short or wide screens (Expand canvas scaling, corner HUD, modal cards).
- Added: Turkish, Russian and Spanish with a flag language picker; English default; start language from Bridge.
- Added: ranking button always visible, with an honest fallback when the platform has no global list.
- Removed: Esc as a pause key (1.5.2).

## E. Open items and risks
1. **SaaS leaderboard** `planet_defender_best_score` must be created in the console (the token is in the config).
2. Real ad behaviour, the live leaderboard, platform mute and the hidden tab are implemented against the v2.4.0 source but not exercised on Playgama.
3. No phone or non-Chromium browser testing yet; no FPS or memory measurement.
4. Planet texture licence to confirm (see C, 4.2.1).
5. The UI font is Liberation Sans (TMP default). A display font (e.g. Orbitron or Exo 2, OFL) would match the design board better; this needs a download approval.
