# Planet Defender

Endless 2D arcade defence for Playgama (WebGL, desktop + mobile), built from `Planet_Defender_GDD_v1.0`.
Unity **6000.4.8f1**, URP 17.4 (2D, HDR bloom), DOTween, Playgama Bridge v2.4.

Shoot the meteors, let the gold through. Gold that lands is Session Gold for upgrades at each level-up; whatever is
left at the end goes to the Bank for unlocking the other seven planets.

## Open / rebuild
1. Open the folder in Unity Hub with 6000.4.8f1 (WebGL module installed).
2. **Planet Defender → Build Everything** regenerates every derived asset from code: URP pipeline, procedural UI and
   FX sprites, materials, particle prefabs, the audio library, the post-FX profile, `Resources/GameAssets` and
   `Scenes/Game.unity`. Run it again after changing the builder; it is safe to repeat.
3. Press Play in `Assets/PlanetDefender/Scenes/Game.unity`.

Batch equivalents:
```
Unity -batchmode -projectPath . -buildTarget WebGL -executeMethod PlanetDefender.EditorTools.PlanetDefenderBuilder.BatchBuild
Unity -batchmode -projectPath . -buildTarget WebGL -executeMethod PlanetDefender.EditorTools.SmokeCapture.Run      # play-mode rule checks + screenshots in Logs/Captures
Unity -batchmode -projectPath . -buildTarget WebGL -executeMethod PlanetDefender.EditorTools.PlaygamaWebBuild.BatchBuild   # Builds/Playgama/*.zip
```

## Where things are
| Area | Path |
|---|---|
| Balance (all GDD numbers) | `Assets/PlanetDefender/Resources/Config/game_config.json` |
| State machine and run flow | `Scripts/Runtime/Gameplay/GameManager.cs` |
| Playfield (spawning, combat, upgrades, passives) | `Scripts/Runtime/Gameplay/Arena.cs`, `SpawnDirector.cs`, `RunState.cs`, `UpgradeShop.cs` |
| Save / economy / leaderboard / ads | `Scripts/Runtime/Services/` (Bridge Storage key `pd_save_v1`) |
| UI (built in code, design-board units 390 × 844) | `Scripts/Runtime/UI/` |
| Game feel (shake, hit-stop, post pulses, popups) | `Scripts/Runtime/FX/` |
| Shaders | `Assets/PlanetDefender/Shaders/` (planet sphere, procedural meteor, gold, ring, starfield, shield, laser, glow) |
| Asset generator / web build / smoke test | `Scripts/Editor/` |

## Before publishing
- Put the real SaaS public token in `Assets/WebGLTemplates/Bridge/playgama-bridge-config.json` and create the
  `planet_defender_best_score` leaderboard in the Playgama dashboard (GDD §11.2).
- Check the planet texture source and attribution in `CREDITS.md`.

The original 2020 project is kept untouched in `_Legacy2019/` (outside `Assets`, so Unity ignores it).
