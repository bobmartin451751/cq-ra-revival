# Phase 17 - engineers and building capture (spec extras, part 2)

## What exists

| Piece | Files |
|---|---|
| Engineer unit kind and stats | `UnitComponents.cs`, `ContentDatabase.cs`, `content.json` |
| Engineer mesh | `ProceduralMeshes.Engineer` |
| Capture command | `BaseCommands.TryCaptureBuilding` |
| Sidebar CAPTURE action + right-click flow | `SelectionController.cs` |
| Tests | `Assets/Tests/PlayMode/EngineerCapturePlayModeTests.cs` |

## Engineer

| Stat | Value |
|---|---:|
| Cost | 150 |
| Build time | 4 s |
| Produced by | Barracks |
| Health | 40 |
| Armour | 0% |
| Speed | 5 m/s |
| Vision | 30 m |
| Weapon | none |

Hotkey: **G**. Engineers are fragile and unarmed; they exist to capture buildings.

## Capturing

1. Select an Engineer.
2. Click **CAPTURE** in the sidebar.
3. Right-click an enemy building within **10 m**.

On success:

- the building switches to the Engineer's team,
- its production queue is cleared,
- its rally point is reset toward the map centre,
- repair stops,
- its team material is refreshed,
- the Engineer is consumed.

Failures: not an engineer, not a building, already yours, or too far. The Engineer
survives every failed attempt.

Captured buildings immediately contribute power, can produce units for their new owner,
and act as refineries for the new team's harvesters. Fog and vision update automatically
because they read `BuildingInfo.Team`.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 53/53, PlayMode 49/49.

### Covered by tests

- An Engineer captures an enemy barracks within range, the building changes team, the
  queue is cleared and the Engineer is consumed.
- Capturing from beyond 10 m fails with `TooFar`, keeps the Engineer and leaves the
  building owned by the enemy.

## Known limits (deliberate for Phase 17)

- No neutral/third-party buildings yet; engineers capture enemy buildings only.
- No repair-bridge/tech-capture variants.
- The AI does not train or use engineers yet.
- No capture animation or sound.
