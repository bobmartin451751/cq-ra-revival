# C&Q R-A Revival

A 3D real-time strategy game built on Unity 6.3 LTS + DOTS.

Genre-faithful to classic Command & Conquer — base building, ore economy, MCV deploy,
tank rushes — but with original factions, units, art, and names.

**The hook: the AI plays by exactly the same rules you do.** No economy bonuses,
no fog-of-war cheating, no hidden stat modifiers. Difficulty comes from decision quality
alone. The replay browser's AI-vision overlay lets you verify it.

## Requirements

- Unity **6.3 LTS** (`6000.3.28f1`) with Windows Build Support (Mono + IL2CPP)
- The editor path is resolved from `$env:UNITY_EDITOR`, defaulting to
  `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`

## Quick start

```powershell
.\tools\screenshot.ps1      # boots the scene, writes .temp/screens/phase1.png
.\tools\run-tests.ps1       # EditMode + PlayMode suites
.\tools\build.ps1           # Windows player -> Builds/Windows
```

Open the project in Unity Hub and play `Assets/Scenes/Main.unity` to run it normally.

## Layout

```
Assets/
  Editor/            headless project setup, scene generation, capture, build
  Scripts/
    Core/            bootstrap, screenshot rig
    Sim/             ECS simulation (movement, combat, economy, vision, pathfinding)
    Content/         JSON -> BlobAsset content pipeline
    View/            rendering, camera, selection visuals
    UI/              UI Toolkit controllers
    Mission/         trigger + objective system
  Data/              factions, units, buildings, ai tunables, maps, missions, strings
  Mods/              load-orderable content
  Tests/             EditMode + PlayMode suites
  Scenes/Main.unity  single bootstrap scene
docs/spec.md         frozen v1 scope
tools/               headless scripts
```

## Status

| Phase | State |
|---|---|
| 0 — toolchain | ✅ verified headless |
| 1 — scaffold | ✅ scene, camera, tests, screenshot loop |

See `docs/spec.md` for the full roadmap and frozen scope.
