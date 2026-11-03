# Planet Defender — Playgama QA checklist

Status as of 2026-11-03, build `PlanetDefender_Playgama_v1.0.zip`.
**RUN** = executed, with the result shown. **NOT RUN** = not executed; still needs a person or the Playgama console.

The browser runs used the Claude desktop browser pane (Chromium) against `python3 -m http.server` on localhost, with
Bridge on its `mock` platform. When the pane is hidden the page is throttled, so **no FPS, timing or feel results are
valid from it**.

## Automated (Unity batch play mode, `SmokeCapture`)
| Check | Result |
|---|---|
| C# compile, WebGL target (Bridge code active) | RUN — 0 errors |
| Default language English; new player owns only Earth; Earth 3 HP | RUN — PASS |
| XP curve 100 / 145 / 200 / 265; upgrade price `ceil(p × 1.65^tier)` | RUN — PASS |
| Language switch rebuilds the UI (Russian) | RUN — PASS |
| Run starts at Level 1 / XP 0 / 3 HP; kills grant score + XP | RUN — PASS |
| Full XP bar freezes the game (time scale 0) and offers 3 cards | RUN — PASS |
| Buying spends session gold and applies the tier; the run resumes | RUN — PASS |
| **Skip by a real pointer click** closes the level-up screen and resumes | RUN — PASS (regression fixed: DOTween recycling killed the screen's fade) |
| Skip stays on screen when a finger lifts over it as the level-up screen opens | RUN — PASS (regression: Skip drifted off screen) |
| Main menu: leaving it or pressing buttons mid-entrance, 4 visits → every element back at its layout position | RUN — PASS (regression: Play drifted under Planets/Ranking) |
| Pause and resume (3-2-1 countdown) | RUN — PASS |
| Last life lost → revive offered once; declining → game over | RUN — PASS |
| Unspent session gold banked once; second settlement of the same run ignored; snapshot cleared; best score saved | RUN — PASS |
| Screens at 4 aspect ratios (9:19.5 phone, 9:16 phone, 3:4 tablet, 16:9 desktop): menu, settings, language, Russian menu, shop, leaderboard, HUD (worst-case 7-digit score), level-up, pause, game over | RUN — no overlap, clipping or overflow seen |
| VFX contact sheet (every particle prefab) | RUN — round, no square artefacts |
| WebGL release build + ZIP | RUN — Succeeded, 0 errors |

## Browser (localhost, Bridge mock)
| Scenario | Result |
|---|---|
| Boot, menu, real planet textures, meteors, HUD | RUN — PASS |
| Mouse input reaches Unity (hold to fire, PLAY) | RUN — PASS (the automation tool's zero-length click is ignored; a real press works) |
| Focus lost / pane hidden → automatic pause | RUN — PASS |
| Language flag button + picker | RUN in play mode; browser NOT RUN by hand |
| Interstitial on level-up / RETRY / PLANETS / menu | NOT RUN: the mock reports interstitials unsupported, so the flow continues (that fallback is covered) |
| Rewarded revive (real ad) | NOT RUN: the mock reports rewarded unsupported, so the offer is hidden and the run goes to game over (as designed) |
| Leaderboard `in_game` list and `setScore` | NOT RUN: needs the SaaS token and the Playgama QA tool (two accounts, GDD §11.2) |
| Platform audio disabled → silent, also after reload | NOT RUN with a real platform |
| Tab hidden → sound stops | Implemented (Bridge pause + template backstop); NOT RUN |
| Reload mid-run → "Continue run" restores level/score/gold/upgrades | Logic in place (snapshot at level-up/pause); NOT RUN in the browser |
| Real phones (iOS Safari, Android Chrome): touch aim, no long-press menu, no scroll | NOT RUN |
| Firefox / Safari / Edge / Opera | NOT RUN (Chromium only) |
| FPS, memory, load time | NOT RUN |

## Before submitting (manual)
- [x] SaaS public token in `playgama-bridge-config.json`; ZIP rebuilt.
- [ ] Create the `planet_defender_best_score` leaderboard in Bridge SaaS → Leaderboards.
- [ ] Upload the draft (title, both orientations, EN/TR/RU/ES, covers, texts).
- [ ] Console **Test Game**: interstitial at level-up and on RETRY/PLANETS/menu; rewarded revive grants +1 life once, and cancelling grants nothing.
- [ ] Leaderboard with two QA accounts: a new best is submitted and appears; the ranking screen lists entries.
- [ ] Platform mute and tab switch silence the game; resume keeps progress.
- [ ] Phones: iOS Safari, Android Chrome (touch aim, portrait + landscape, no page scroll).
- [ ] Desktop: Chrome, Firefox, Safari, Edge; resize to 1:1.
