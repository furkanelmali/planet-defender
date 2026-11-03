# Planet Defender — Playgama WebGL build instructions

Editor: **Unity 6000.4.8f1** with **WebGL Build Support**. The project's active target is already WebGL.

## What is in the project
| Piece | Location |
|---|---|
| Playgama Bridge SDK | `Packages/manifest.json` → `com.playgama.bridge` = `https://github.com/playgama/bridge-unity.git#v2.4.0` |
| Bridge WebGL template | `Assets/WebGLTemplates/Bridge/` (official files; title "Planet Defender"; no scroll, selection, context menu or iOS callout; hidden-tab audio backstop) |
| Bridge config | `Assets/WebGLTemplates/Bridge/playgama-bridge-config.json`: 3 interstitial placements, 1 rewarded placement, SaaS leaderboard |
| Platform layer | `Assets/PlanetDefender/Scripts/Runtime/Services/` (`PlaygamaBridgeAdapter`, `SaveService`, `LeaderboardService`, `EconomyService`, `AudioService`) |
| Build script | `Assets/PlanetDefender/Scripts/Editor/PlaygamaWebBuild.cs` |
| Asset/scene generator | `Assets/PlanetDefender/Scripts/Editor/PlanetDefenderBuilder.cs` |
| Play-mode checks | `Assets/PlanetDefender/Scripts/Editor/SmokeCapture.cs` |
| Covers | `Assets/PlanetDefender/Scripts/Editor/PlaygamaCoverCapture.cs` → `Documentation/Playgama/Covers/` |

## Before the first upload
1. **SaaS leaderboard:** the public token is already in `saas.publicToken` and the provider is on for `playgama` and
   `qa_tool`. In the Playgama Developer Console → the game → **Bridge SaaS → Leaderboards**, create the leaderboard with
   ID **`planet_defender_best_score`** (exactly this string; the game reads it from the config root `leaderboards`).

## Build from the editor
- **Planet Defender → Build Everything** (only after changing the builder: sprites, materials, VFX, audio, scene).
- **Planet Defender → Playgama → Build WebGL + ZIP**: configures the WebGL settings, builds `Scenes/Game.unity` to
  `Builds/Playgama/WebGL/`, checks that `index.html`, the config and the Bridge scripts are present, and writes
  `Builds/Playgama/PlanetDefender_Playgama_v1.0.zip` with `index.html` at the archive root.

## Build from the command line (editor closed)
```sh
"/Applications/Unity/Hub/Editor/6000.4.8f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath "$PWD" -buildTarget WebGL \
  -executeMethod PlanetDefender.EditorTools.PlaygamaWebBuild.BatchBuild -logFile Logs/webgl.log
```
Success markers: `PD_WEBGL_BUILD Succeeded`, `PD_ZIP_OK`.

## WebGL player settings (`PlaygamaWebBuild.Configure`)
- Template `PROJECT:Bridge`, no splash screen
- Gzip with **Decompression Fallback on** (5.1.1); **data caching off** (no engine IndexedDB cache, 3.2.1)
- Exceptions: explicitly thrown only; no diagnostics overlay
- Memory 64 MB initial, 1024 MB max, geometric growth (below the 2 GB start-up limit, 1.3.3)
- Managed stripping Medium; IL2CPP optimize-size, Master
- URP: HDR + bloom, no shadows, no depth or opaque textures, MSAA off; linear colour space

## Local test
```sh
cd Builds/Playgama/WebGL && python3 -m http.server 8080
```
Then open `http://localhost:8080/`. Bridge runs its `mock` platform on localhost: interstitials and rewarded ads are
reported unsupported, so the game continues immediately (and dying goes straight to game over), and the leaderboard is
`not_available`. Real ads and the global leaderboard can only be checked with **Test Game** in the Playgama console.

## Playgama console (manual)
1. New game draft → upload `PlanetDefender_Playgama_v1.0.zip`.
2. Title **Planet Defender**. Devices: mobile + desktop. Orientation: **both**. Languages: **English, Turkish, Russian, Spanish**.
3. Upload the three covers from `Documentation/Playgama/Covers/`, and the texts from `PLAYGAMA_STORE_LISTING.md`.
4. Run **Test Game / QA** and work through the manual part of `PLAYGAMA_QA_CHECKLIST.md`, then submit.
