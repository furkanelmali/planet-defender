# Planet Defender — goals and hooks

Baseline (3 Nov, one day, 32 gameplays, so a small sample): 99 launches → 69 game ready → 32 at 30 s+ → 20 at 60 s+. Average play: desktop 1m48s, Android 4m52s, iOS 9m47s.
Priority 1: more players past 30 s. Priority 2: longer play for those who get there, without a large loss of ad income.

All numbers below are tunable in `Resources/Config/game_config.json`.

## First 30 seconds
| Moment | What the player gets | Why |
|---|---|---|
| Load ends | The run itself, no menu (new players only) | Removes the choice between load and play |
| Before the first press | One marked meteor with "SHOOT IT!"; time and spawns wait | The opening cannot be missed or lost |
| ~2 s | First kill | Immediate result of the first action |
| ~5 s | Gold arrives; shots pass through it during the assist | The rule is taught without a penalty |
| ~20 s | First level-up: all three cards free | The first reward is never out of reach |
| 30 s | Sector 2 banner and +15 gold; the first mission lands around here | A payoff inside the 30 s window |
| to 45 s | The last life cannot be lost | Nobody loses before they understand the game |

## Goal layers
| Horizon | Hook | Rule |
|---|---|---|
| Seconds | XP bar, kill combo | Existing |
| ~1 minute | **Sectors**: 30 s, then every 60 s; bonus 15, 30, 45... gold; the HUD counts down to the next | New meteor types are announced as they unlock; a giant meteor arrives with each sector from 2:00 |
| Minutes | **Missions**: three always active, nearest one on the HUD and menu | Pays session gold when finished; the next type comes in from a rotation of seven, one tier harder |
| One run | **Best score alert** during play; time, sector and the gap to the best on the results | Near misses stay visible |
| A few runs | **Next planet** progress bar on the results and in Goals; the Planets button pulses when one is affordable | Existing economy, now always in sight |
| Tomorrow | **Daily reward**: 50, 75, 100, 150, 200, 300, 500; shows tomorrow's amount | Missing a day restarts at day one |

## Desktop
Mouse players no longer have to hold a button for the whole run: Space latches auto-fire. The first hint is written for a mouse.

## How to judge it
Compare builds by launches, not by plays: ads per launch (0.81 today), game ready → 30 s+ (46% today), 30 s+ → 60 s+ (62.5% today), desktop play time (1m48s today), close after ad (17.1% today), and day-1 retention once it exists.
Wait for at least 100 gameplays per build. Own test launches should be kept out where possible.
