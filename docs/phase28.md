# Phase 28 - Verdant Sower and mines

## What exists

| Piece | Files |
|---|---|
| Sower unit kind and mine components | `UnitComponents.cs` |
| Stats | `ContentDatabase.cs`, `content.json` |
| Verdant roster slot | `ProductionComponents.cs` |
| Mesh and colours | `ProceduralMeshes.Sower/Mine`, `UnitSpawner`, `BaseRenderer` |
| Mine spawning | `BuildingFactory.SpawnMine`, `BaseCommands.TryLayMine` |
| Mine behaviour | `MineSystem.cs` |
| Sidebar action | `SelectionController.LayMineSelected` |
| Tests | `SowerMinePlayModeTests.cs` |

## Sower

| Stat | Value |
|---|---:|
| Faction | Verdant Combine |
| Producer | War Factory |
| Cost | 300 |
| Build time | 6 s |
| Health | 180 |
| Armour | 25% |
| Speed | 7 m/s |
| Vision | 28 m |
| Weapon | none |
| Hotkey | `U` (Verdant roster slot 8) |

## Mines

- Select a Sower → **LAY MINE** in the command strip.
- A mine is placed at the Sower's position; the Sower has a **4-second cooldown**.
- Mines are team-owned and ignore friendly units.
- **Trigger radius 3 m**, **blast radius 5 m**, up to **80 damage** with distance falloff.
- Armour and shields apply normally.
- The mine is destroyed when it detonates.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 65/65, PlayMode 59/59.

### Covered by tests

- A Sower lays a mine and starts its cooldown.
- An enemy entering the trigger radius detonates it, removing the mine and damaging the
  enemy.

## Known limits

- No mine visuals for arming/blinking, no explosion effect.
- Mines are not hidden from the enemy; no stealth mines.
- The AI does not build or use Sowers.
- Mines cannot be manually detonated or cleared by engineers.
