# Phase 16 - supply crates (spec extras, part 1)

## What exists

| Piece | Files |
|---|---|
| Crate component, pickup system | `Assets/Scripts/Sim/Base/ProductionComponents.cs`, `CrateSystem.cs` |
| Crate mesh, factory and material | `ProceduralMeshes.Crate`, `BuildingFactory.SpawnCrate`, `BaseRenderer` |
| Deterministic spawning | `EconomyAuthoring.cs` |
| Fog hiding for unexplored crates | `FogVisibilitySystem.cs` |
| Runtime reset | `SkirmishCommands.ResetRuntimeState` |
| Tests | `Assets/Tests/PlayMode/CratePlayModeTests.cs` |

## Behaviour

- Six crates spawn at deterministic map positions at match start, provided the cell is
  walkable.
- Two kinds:
  - **Credit crate** - 250 credits to the collecting team.
  - **Repair crate** - 80 health to the collecting unit, capped at max health.
- Any unit within 3 m (XZ distance) picks up the crate; it is then removed.
- Crates do not block navigation or fog, and unexplored crates are hidden by the shroud.
- Crates are cleared on scene reload with the rest of the runtime state.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 53/53, PlayMode 47/47.

### Covered by tests

- A credit crate pays 250 to the collector's team and is removed.
- A repair crate heals up to max health and is removed.

## Known limits (deliberate for Phase 16)

- Crates are static pickups; no random/periodic respawn.
- No visual pickup effect or sound yet.
- Air units can collect crates by flying over them.
- Other spec extras (engineers/captureables, garrison, transports, stealth/detection,
  tech capture, directional armour, terrain LOS, destructible bridges, mutators) remain.
