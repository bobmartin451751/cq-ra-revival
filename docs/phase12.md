# Phase 12 - air units (gunships)

## What exists

| Piece | Files |
|---|---|
| Gunship unit kind, air component | `UnitComponents.cs` |
| Combat/vision/speed tuning | `CombatComponents.cs`, `UnitFactory.cs`, `FogComponents.cs` |
| Air movement (altitude, no nav) | `UnitMovementSystem.cs` |
| Production entry + hotkey | `ProductionComponents.cs`, `SelectionController.cs` |
| Procedural gunship mesh | `ProceduralMeshes.cs` |
| AI gunship production | `AiSystem.cs` |
| Capture demo | `ScreenshotCapture.cs` (`-air`), `tools/screenshot.ps1` (`-Air`) |

## Gunship

| Stat | Value |
|---|---:|
| Cost | 900 |
| Build time | 10 s |
| Produced by | War Factory |
| Health | 180 |
| Armour | 20% |
| Damage | 34 |
| Range | 22 m |
| Cooldown | 1.1 s |
| Speed | 16 m/s |
| Vision | 44 m |
| Cruise altitude | 18 m |

## Air movement

- Gunships carry an `AirUnit { Altitude = 18 }` component.
- `UnitMovementSystem` skips flow-field lookups, nav walkability and wall sliding for
  air units; they steer straight at their destination.
- Ground height is still sampled so the aircraft follows the terrain contour at a fixed
  altitude above it.
- Separation is layer-aware: air units never push ground units and ground units never
  push air units. Air-to-air spacing is currently ignored.
- Fog, combat, targeting, production queues and rally points all work unchanged.

## Production and controls

- `B` queues a Gunship from the selected War Factory (or the first War Factory with
  queue space).
- Gunships spawn at the rally point like any other unit and then climb to altitude.
- Hardened and Brutal AI now build a gunship once it has four or more ground units.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase12-air.png -Air -WaitFrames 1600
```

`-Air` builds both bases and flies three gunships per team across the map.

Suites: EditMode 47/47, PlayMode 42/42.

### Covered by tests

- Gunships are built at the War Factory, cost more than tanks and take real time.
- They are armed, faster and more fragile than tanks, and out-scout ground units.
- A gunship crosses a solid blocked nav column that ground units cannot pass.
- A gunship flies above the terrain (Y > 5 m).
- A War Factory produces a Gunship with the `AirUnit` component.

## Known limits (deliberate for Phase 12)

- No dedicated anti-air restriction; every armed unit can shoot at gunships.
- No banking, hovering animation, rotor spin or crash/wreck visuals.
- No air-vs-air special rules, missiles or bombing runs.
- Air units ignore building collisions entirely; they can overfly a base.
