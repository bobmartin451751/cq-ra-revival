# Phase 9 - first playable skirmish

## What exists

| Piece | Files |
|---|---|
| Skirmish menu, pause, AI start, result overlay, restart | `Assets/Scripts/Core/SkirmishController.cs` |
| Match setup + runtime-state reset commands | `Assets/Scripts/Sim/AI/SkirmishCommands.cs` |
| Scene wiring | `Assets/Editor/SceneBuilder.cs` (generated `Skirmish` object) |
| Tests | `Assets/Tests/EditMode/Phase9Tests.cs`, `Assets/Tests/PlayMode/SkirmishPlayModeTests.cs` |

This is the first build that can be played from start to finish: choose a difficulty,
fight the AI under identical rules, win or lose, then restart.

## Playing

1. Open `Assets/Scenes/Main.unity` in the editor or run the Windows build.
2. The match starts paused on the **SKIRMISH** menu.
3. Pick a difficulty (Recruit / Veteran / Hardened / Brutal) and press **START MATCH**.
4. The player always controls team 0; team 1 is commanded by the AI.
5. When one side has no units and no buildings left, the match ends and shows
   **VICTORY**, **DEFEAT** or **DRAW** with a **RESTART** button.

The AI uses the same credits, build radius, queues, harvester loop and combat orders as
the player. Difficulty only changes its decision quality (see `docs/phase8.md`).

## Match lifecycle

- **Menu:** `Time.timeScale = 0`, selection and camera rig disabled. IMGUI buttons choose
  the difficulty and start the match.
- **Start:** time scale restored, camera/selection enabled, and `SkirmishCommands.StartMatch`
  replaces any existing AI commander with exactly one commander for team 1.
- **End:** when `MatchState.GameOver` is set, the world pauses, selection is disabled and
  the result overlay appears.
- **Restart:** reloads the active scene. Because the ECS world survives a scene reload,
  `SkirmishController.Awake` calls `SkirmishCommands.ResetRuntimeState` first, clearing
  units, buildings, ore, economy, match state, AI commanders and flow fields. The scene's
  `NavGridAuthoring`, `UnitSpawner` and `EconomyAuthoring` then rebuild the match.

Screenshot/batch runs (`-screenshot`) skip the menu and auto-start the match so all
existing capture rigs continue to work.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase9-skirmish.png -Ai -AiLevel 2 -WaitFrames 5000
```

Suites: EditMode 38/38, PlayMode 35/35.

### Covered by tests

- Player and AI team ids are stable (0 and 1).
- The result text maps winner 0/1/255 to VICTORY/DEFEAT/DRAW.
- Starting a match creates exactly one AI commander on team 1, never on the player team,
  and restarting replaces it rather than stacking commanders.
- `ResetRuntimeState` clears units, buildings, ore, economy, match state and commanders.

## Known limits (deliberate for Phase 9)

- One map and one mode: 1v1 skirmish against the AI. No map picker or lobby.
- No campaign, save/load or replays yet.
- The AI still sees the whole map because fog of war has not landed; it must be
  restricted to visible targets in the vision phase.
- The first playable loop uses the Phase 5-7 unit/building set; garrison, captureables,
  transports and other extras remain on the backlog.
- The menu is IMGUI (not captured by the screenshot rig) and the HUD still uses IMGUI;
  the UI Toolkit conversion is a later phase.
