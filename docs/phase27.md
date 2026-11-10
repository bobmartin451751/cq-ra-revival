# Phase 27 - Ashfall Rig repair vehicle

## What exists

| Piece | Files |
|---|---|
| Rig unit kind | `UnitComponents.cs` |
| Stats | `ContentDatabase.cs`, `content.json` |
| Ashfall roster entry | `ProductionComponents.cs` |
| Mesh and colours | `ProceduralMeshes.Rig`, `UnitSpawner` |
| Repair aura | `RepairAuraSystem.cs` |
| Tests | `RepairAuraPlayModeTests.cs` |

## Rig

| Stat | Value |
|---|---:|
| Faction | Ashfall Compact |
| Producer | War Factory |
| Cost | 350 |
| Build time | 7 s |
| Health | 260 |
| Armour | 30% |
| Speed | 6 m/s |
| Vision | 26 m |
| Weapon | none |
| Hotkey | `U` (Ashfall roster slot 8) |

## Repair aura

- A **Rig automatically repairs friendly mechanical units within 10 m** at **10 HP/s**.
- Mechanical units: tanks, harvesters, MCVs, gunships, Thumpers, Skyfalls and other Rigs.
- Infantry and engineers are ignored.
- Repair never exceeds maximum health, and multiple Rigs stack.
- The Rig does not repair itself.

The aura is always on; no command is needed.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 65/65, PlayMode 58/58.

### Covered by tests

- A Rig heals a nearby damaged friendly tank and never overheals it.
- Ashfall's roster now contains both Thumper and Rig; other faction rosters are unchanged.

## Known limits

- No repair beam/visual effect or sound.
- The AI does not build Rigs yet.
- Repair is instant-tick aura healing, not a targeted repair ability.
- Rigs do not repair buildings.
