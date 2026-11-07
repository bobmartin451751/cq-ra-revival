# Phase 3 — movement and flow-field pathfinding

## What exists

| System | Files |
|---|---|
| Simulation-owned terrain + nav data | `Assets/Scripts/Sim/Navigation/NavGridTypes.cs`, `NavGridAuthoring.cs` |
| Walkability grid builder | `Assets/Scripts/Sim/Navigation/NavGridBuilder.cs` |
| Flow-field builder (Burst, Dijkstra) | `Assets/Scripts/Sim/Navigation/FlowFieldBuilder.cs` |
| Grid queries for input code | `Assets/Scripts/Sim/Navigation/NavGridQuery.cs` |
| Flow-field cache system | `Assets/Scripts/Sim/Units/FlowFieldSystem.cs` |
| Deterministic steering + avoidance | `Assets/Scripts/Sim/Units/UnitMovementSystem.cs` |
| Move order component / motion state | `Assets/Scripts/Sim/Units/MovementComponents.cs` |
| Heightmap ray picking | `Assets/Scripts/View/TerrainPicker.cs` |
| Order marker + procedural ruins | `Assets/Scripts/View/MoveOrderMarker.cs`, `Scenery.cs` |

No Unity Physics, no NavMesh and no colliders are used for units. The scene is still
generated entirely from code by `CQRa.EditorTools.SceneBuilder.CreateMainScene`.

## Navigation data

At scene start `NavGridAuthoring` builds one simulation entity that owns:

- `TerrainInfo` + `TerrainHeightSample` buffer — the authoritative heightmap copy
  (129 × 129 bilinear samples) so gameplay never touches the render MonoBehaviour.
- `NavGridInfo` + `NavCell` buffer — a 180 × 180 grid at 2 m per cell over the
  360 m map.
- A cell is blocked when it is inside the map margin, sits on a slope steeper than
  **0.5** (≈ 26.5°), or is inside a circular blocker.

Five ruined outcrops are saved in the scene as `Scenery` obstacle specs and mirrored
into the grid as blockers (`radius + 2.5 m` padding). The same specs drive the
procedural rock meshes, so visuals and navigation cannot drift apart.

The current map has 27 766 / 32 400 walkable cells; the outer hills form a natural
boundary without a hard invisible wall.

## Flow fields

`FlowFieldSystem` runs in `FixedStepSimulationSystemGroup` before movement.

1. Every unit with an order and no valid field requests its destination cell.
2. The destination is snapped to the nearest walkable cell (deterministic nearest-cell
   search, ties broken by index).
3. The system caches one field per destination cell (up to 16). Re-using a field means
   a 200-unit attack move costs a single Dijkstra pass.
4. `BuildFlowFieldJob` is a Burst-compiled `IJob` that produces an 8-connected
   integration field and a byte-per-cell direction field. Ties in the priority heap are
   broken by cell index, so the output is identical across runs and machines.
5. Diagonal moves cost 14, straight moves 10, and diagonal corner-cutting through
   blocked cells is forbidden.

Direction bytes are stored in a `DynamicBuffer<FlowFieldDirection>` on the field entity.

## Steering

`UnitMovementSystem` runs after the flow field pass, also in the fixed-step group.

- One byte lookup per unit per tick gives the next direction; direct steering blends
  in over the last few metres so units settle around the destination.
- Separation uses a deterministic 8 m uniform spatial hash (no O(n²) neighbour scan).
- Velocity is exponentially smoothed; yaw turns toward travel direction at 7.5 rad/s.
- A blocked next step slides along the still-free axis instead of penetrating it.
- Ground height comes from the simulation height buffer; units stay planted on the
  terrain without raycasts.

Tuning lives at the top of `UnitMovementSystem`: turn rate, separation strength,
spatial-hash cell size, velocity smoothing.

## Orders

Right-click on the terrain issues a move order to every selected unit:

- `TerrainPicker` marches the camera ray against the heightmap and bisects the hit.
- `NavGridQuery` snaps the click to the nearest walkable cell.
- The selection writes `MoveTarget { Destination, HasOrder = 1, Field = null }`.
- `MoveOrderMarker` shows a short gold ground ping (presentation only).

The HUD now reports both selected and currently moving unit counts.

## Determinism notes

- All gameplay runs at **30 Hz** (`GameBootstrap` sets both `Time.fixedDeltaTime` and
  `FixedStepSimulationSystemGroup.Timestep`).
- Single-threaded simulation; only the flow-field job is Burst-compiled and it is
  completed before movement reads it.
- No use of `UnityEngine.Random`, physics, or frame delta time in gameplay code.

The managed movement loop is intentionally small for Phase 3. Unit counts beyond a few
hundred will move to an `ISystem` + Burst job in the performance pass; the data layout
(component per unit, cached fields) already supports that.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase3-scene.png
.\tools\screenshot.ps1 -Out .temp/screens/phase3-move.png `
  -MoveTo "-10,6" -MoveTeam 0 -WaitFrames 170
.\tools\screenshot.ps1 -Out .temp/screens/phase3-arrive.png `
  -MoveTo "-31,-4" -MoveTeam 0 -WaitFrames 620
```

The `-moveto` capture mode selects a team, issues a real move order, clears the
selection, and renders after the requested number of frames. It uses the same ECS
systems as normal play.

Suites: EditMode 15/15, PlayMode 5/5.

### Covered by tests

- Walkability: interior, map margin, steep cliff, obstacle disc + padding.
- Nearest-walkable snapping.
- Flow-field direction completeness, wall routing, no blocked cells, determinism.
- Bilinear terrain sampling.
- Factory adds `MoveTarget` / `UnitMotion`.
- A tank routes through a wall gap, never enters a blocked cell, and stops within
  its arrival radius.
