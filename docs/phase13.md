# Phase 13 - Command & Conquer-style command sidebar

## What exists

| Piece | Files |
|---|---|
| Right-hand COMMAND sidebar, action buttons, tabs | `Assets/Scripts/View/SelectionController.cs` |
| Armed-command model and right-click execution | `SelectionController.cs` |
| Deploy MCV at a chosen site | `Assets/Scripts/Sim/Base/BaseCommands.cs` (`TryDeployMcvAt`) |
| Asset reorganisation | `Assets/Art/Materials/` |
| Tests | `Assets/Tests/PlayMode/DeployPlayModeTests.cs` |

## The interaction

The sidebar on the right is the C&C-style command box.

- **Credits, power and team counts** at the top.
- **Radar**: a small live minimap showing friendly units/buildings and only fog-visible
  enemies.
- **Selection summary**: unit/building name and health (plus `[REPAIRING]`).
- **Action strip** driven by the current selection:
  - MCV → **DEPLOY**
  - production building → **RALLY**
  - units → **STOP**
  - buildings → **REPAIR**, **SELL**
- **STRUCTURES / UNITS tabs** with cost, hotkey and build rows.
- **Queue line** for the selected producer.
- Bottom control legend.

## Armed commands

Clicking **DEPLOY** or **RALLY** arms a command. The button highlights, the HUD explains
what to do, then **right-click the world** to execute it.

- **DEPLOY**: right-click a site within 50 m of the MCV. The site is validated exactly
  like a building placement: walkable centre and rim, no overlap with buildings, units
  or ore. On success the MCV is consumed and the yard appears at the chosen point. A
  rejected site leaves the MCV alive and reports why.
- **RALLY**: right-click terrain to set the rally point of every selected production
  building.
- **Esc** cancels an armed command before cancelling building placement or clearing the
  selection.
- Changing the selection cancels an armed command.
- **REPAIR**, **SELL** and **STOP** execute immediately on the current selection.

The old **D** deploy binding is gone, so **D is camera-pan only**. The sidebar button is
the primary way to deploy, matching C&C.

## HUD input safety

Pointer input over the sidebar never reaches the world:

- no selection drag,
- no move/attack order,
- no building placement,
- no deploy/rally execution.

Keyboard shortcuts still work.

## Asset arrangement

Generated materials now live in **`Assets/Art/Materials/`** and `SceneBuilder` writes
there. The project layout is documented in the README. `Assets/Resources/Data/` is
reserved for the upcoming JSON content and missions.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 47/47, PlayMode 45/45.

### Covered by tests

- Deploying at a chosen site creates the yard at that point and consumes the MCV.
- A deploy site occupied by a unit is rejected and the MCV survives.
- A deploy site beyond 50 m is rejected with `TooFar` and the MCV survives.

Note: the HUD is IMGUI, so the camera screenshot rig does not include the sidebar. Use
the editor or the Windows build to inspect it.

## Known limits (deliberate for Phase 13)

- The sidebar is IMGUI, not UI Toolkit; no animated portraits or icon art yet.
- Radar is display-only; clicking it does not move the camera.
- Walls still place one segment per click (the tool stays armed for chaining).
- No production cancel/refund, no control groups, no tooltips yet.
- Armed abilities other than Deploy/Rally (engineer capture, garrison) arrive with the
  extras phase.
