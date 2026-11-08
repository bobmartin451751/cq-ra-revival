# Phase 11 - defences and walls

## What exists

| Piece | Files |
|---|---|
| Gun Tower and Wall specs | `Assets/Scripts/Sim/Base/BaseComponents.cs` |
| Building meshes and spawn data | `BuildingFactory.cs`, `ProceduralMeshes.cs`, `BaseRenderer.cs` |
| Fog-aware defensive fire | `Assets/Scripts/Sim/Base/DefenseSystem.cs` |
| Build-panel keys 5/6 and wall chaining | `Assets/Scripts/View/SelectionController.cs` |
| AI turret construction | `Assets/Scripts/Sim/AI/AiSystem.cs` |
| Capture demo | `ScreenshotCapture.cs` (`-defense`), `tools/screenshot.ps1` (`-Defense`) |

## Gun Tower

| Stat | Value |
|---|---:|
| Cost | 500 |
| Power draw | 20 |
| Footprint / nav block | 4 m |
| Health | 450 |
| Damage | 18 |
| Range | 28 m |
| Cooldown | 0.7 s |
| Vision | 24 m |

The Gun Tower is a building with a `DefenseWeapon`. `DefenseSystem` runs after the fog
update, acquires the nearest **visible** enemy unit in range and fires hitscan shots with
normal armour mitigation. Shots use the same tracer feed as units, and enemy tracers
remain fog-filtered.

Because the tower's vision (24 m) is shorter than its range (28 m), targets that are in
range but still under the shroud are ignored. The player can see this in practice: an
enemy 26 m away stays safe until it (or another friendly unit) reveals the area.

## Walls

| Stat | Value |
|---|---:|
| Cost | 50 |
| Power draw | 0 |
| Footprint / nav block | 2.2 m |
| Health | 250 |
| Vision | 6 m |

Walls are cheap building-kind structures. Placing one blocks the nav grid under it, so
units path around or attack it. The build tool **stays armed after placing a wall**, so
the player can chain segments by clicking along a line.

Walls and towers are normal buildings: selectable, repairable with `R`, sellable with
`T` (25 and 250 credit refunds), targetable and destructible.

## Controls

- `1` Power Plant
- `2` Refinery
- `3` Barracks
- `4` War Factory
- `5` Gun Tower
- `6` Wall (stays armed for chaining)
- `Esc` cancels the build tool

## AI

AI profiles now include turret counts:

| Tier | Turrets |
|---|---:|
| Recruit | 0 |
| Veteran | 1 |
| Hardened | 2 |
| Brutal | 2 |

The AI places towers once it has credits above 900 and a production base, using the same
`TryAutoPlace`/`TryPlaceBuilding` rules and paying the normal cost.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase11-defenses.png -Defense -WaitFrames 2200
```

`-Defense` builds both bases plus a gun tower and three wall segments per team through the
real build rules.

Suites: EditMode 44/44, PlayMode 40/40.

### Covered by tests

- Gun Tower cost/power/footprint and defensive tuning values.
- Walls are cheaper and smaller than other buildings and cannot be sold for more than cost.
- Wall and tower sell refunds (25 and 250) and sellability.
- A gun tower kills a visible enemy in range.
- A gun tower does not fire at an enemy hidden by fog, then kills it once revealed.
- Placing a wall blocks nav cells; selling it frees them and refunds the correct amount.

## Known limits (deliberate for Phase 11)

- Walls are circular footprints with a fixed orientation; there are no wall gates or
  corner/connection logic.
- Gun towers target units only, not buildings; there is no anti-air yet (planes come in a
  later phase).
- No turret rotation or muzzle animation; towers fire from a fixed muzzle point.
- AI places individual towers, not wall rings.
