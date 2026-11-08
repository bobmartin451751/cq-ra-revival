# C&Q R-A Revival

A 3D real-time strategy game built on Unity 6.3 LTS + DOTS.

Genre-faithful to classic Command & Conquer - base building, ore economy, MCV deploy,
tank rushes - but with original factions, units, art, and names.

**The hook: the AI plays by exactly the same rules you do.** No economy bonuses,
no fog-of-war cheating, no hidden stat modifiers. Difficulty comes from decision quality
alone. The replay browser's AI-vision overlay lets you verify it.

## Requirements

- Unity **6.3 LTS** (`6000.3.28f1`) with Windows Build Support (Mono + IL2CPP)
- The editor path is resolved from `$env:UNITY_EDITOR`, defaulting to
  `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`

## Quick start

```powershell
.\tools\screenshot.ps1                  # boots the scene, writes .temp/screens/phase2.png
.\tools\screenshot.ps1 -SelectAll       # same, with every unit selected
.\tools\screenshot.ps1 -Out .temp/screens/phase3-move.png -MoveTo "-10,6" -WaitFrames 170
.\tools\screenshot.ps1 -Out .temp/screens/phase4-battle.png -Attack -MoveTeam 0 -WaitFrames 800
.\tools\screenshot.ps1 -Out .temp/screens/phase5-base.png -Base -WaitFrames 1400
.\tools\screenshot.ps1 -Out .temp/screens/phase6-army.png -Army -WaitFrames 2400
.\tools\screenshot.ps1 -Out .temp/screens/phase7-siege.png -Siege -WaitFrames 900
.\tools\screenshot.ps1 -Out .temp/screens/phase8-ai.png -Ai -AiLevel 3 -WaitFrames 6000
.\tools\screenshot.ps1 -Out .temp/screens/phase9-skirmish.png -Ai -AiLevel 2 -WaitFrames 5000
.\tools\run-tests.ps1                   # EditMode + PlayMode suites
.\tools\build.ps1                       # Windows player -> Builds/Windows
```

Open the project in Unity Hub and play `Assets/Scenes/Main.unity` to run it normally.
The skirmish menu lets you pick a difficulty and start a match against the AI.

**Controls:** WASD/arrows or middle-drag to pan, wheel to zoom, Q/E to rotate,
edge scroll at the screen border, left-click to select, drag a box to multi-select,
Shift to add, **right-click to move**, **right-click an enemy unit or building to
attack**, **1-4 to arm a building, D to deploy a selected MCV, Z/X/C/V to produce
units, R to repair, T to sell**, Esc to clear/cancel. Right-click with production
buildings selected sets their rally point. See `docs/phase2.md` through
`docs/phase8.md` for details.

## Layout

```
Assets/
  Editor/            headless project setup, scene generation, capture, build
  Scripts/
    Core/            bootstrap, screenshot rig
    Sim/             ECS simulation (movement, navigation, combat, economy, vision)
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
| 0 - toolchain | OK - verified headless |
| 1 - scaffold | OK - scene, camera, tests, screenshot loop |
| 2 - terrain, camera, selection | OK - heightmap, RTS camera rig, click + box select, highlight |
| 3 - movement + pathfinding | OK - nav grid, Burst flow fields, steering, right-click orders |
| 4 - combat core | OK - health, weapons, auto-acquire, attack orders, damage, death, match state |
| 5 - base + economy | OK - MCV deploy, build placement, credits, power, ore, harvester loop |
| 6 - production | OK - barracks/war factory, queues, unit construction, rally points |
| 7 - building combat | OK - buildings targetable/destructible, repair, sell, nav freeing |
| 8 - skirmish AI | OK - deterministic AI commander, build/produce/attack, 4 difficulty tiers |
| 9 - first playable | OK - skirmish menu, difficulty select, AI match, result and restart |

See `docs/spec.md` for the full roadmap and frozen scope.
