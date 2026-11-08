# Phase 10 - fog of war and vision

## What exists

| Piece | Files |
|---|---|
| Fog grid, vision data and math | `Assets/Scripts/Sim/Fog/FogComponents.cs` |
| Per-team fog update system | `Assets/Scripts/Sim/Fog/FogSystem.cs` |
| Fog queries for combat/AI/selection | `Assets/Scripts/Sim/Fog/FogQuery.cs` |
| Enemy/ore visibility masking | `Assets/Scripts/Sim/Fog/FogVisibilitySystem.cs` |
| Shroud/explored rendering | `Assets/Scripts/View/FogRenderer.cs` |
| Vision + fog filtering in combat/AI | `CombatSystem.cs`, `AiSystem.cs`, `SelectionController.cs` |

## Vision

Every unit and building carries a `VisionSource`. Vision is stamped into a per-team fog
grid at **4 m** resolution (90 x 90 cells on the 360 m map). Cells have three states:

- **Never** (0) - black shroud, terrain and enemies hidden.
- **Explored** (1) - remembered terrain, no live enemy information.
- **Visible** (2) - current vision, live enemies shown and targetable.

| Unit | Vision | Building | Vision |
|---|---:|---|---:|
| Tank | 40 m | Construction Yard | 38 m |
| Infantry | 32 m | War Factory | 32 m |
| MCV | 26 m | Refinery / Barracks | 28 m |
| Harvester | 22 m | Power Plant | 24 m |

Visible cells decay to explored at the start of each 30 Hz tick, then all living units
and buildings stamp their own team's grid. Old positions stay explored but stop showing
live enemies.

## Rules

- **Player view.** Friendly entities are always rendered. Enemy units and buildings get a
  `DisableRendering` component while their cell is not visible to team 0. Ore is hidden
  until its cell has been explored.
- **Targeting is fair.** Combat auto-acquisition and AI target selection only consider
  enemies in the acting team's visible cells. The AI has no map-wide knowledge.
- **Explicit orders.** A right-click attack order may chase into fog (the player can
  order a remembered target); auto-acquisition will not start on unseen enemies.
- **Selection.** Enemy entities under fog cannot be picked. Right-clicking an enemy
  requires it to be visible.
- **Tracers.** Enemy tracer lines are suppressed unless their origin is visible to the
  player, so shots do not reveal hidden positions.
- **Rendering.** `FogRenderer` builds two draped meshes (never-seen shroud and explored
  dimming) from the player's grid, updating at ~8 Hz.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase10-fog.png -Ai -AiLevel 2 -WaitFrames 2500
```

Suites: EditMode 41/41, PlayMode 37/37.

### Covered by tests

- Vision radii are positive and tank scouting beats harvester scouting.
- Fog math reads states, decays visible to explored and treats out-of-bounds as never seen.
- A moving scout reveals its own position, leaves explored cells behind, and does not see
  distant cells.
- Enemies outside player vision are hidden and become rendered when they enter vision.

## Known limits (deliberate for Phase 10)

- No terrain/ruin line-of-sight blocking; vision circles pass through obstacles.
- No minimap or radar.
- Fog is a blocky 4 m grid with hard cell edges.
- The AI does not deliberately scout; it reacts to what its units can see.
- No shroud reveal from allied players (single-player only).
