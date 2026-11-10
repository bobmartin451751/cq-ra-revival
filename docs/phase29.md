# Phase 29 - naval units and faction building colours

## Naval

### Lake
- A circular lake is carved into the western edge of the map at **(-100, 0)**,
  **radius 28 m**, **water level 0.55 m**.
- Ground navigation blocks the lake; a translucent water plane renders on top.
- Water is a fixed, obstacle-free basin (no shore pathing yet).

### Naval Yard
- New building: **Naval Yard**, cost **800**, power draw **40**, HP **700**.
- Buildable from the sidebar with **7**.
- Produces ships. Placed on land like other structures.

### Ships
| Ship | Cost | Build | HP | Armour | Damage | Range | Speed | Vision |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Patrol Boat | 600 | 8 s | 320 | 30% | 40 | 26 | 9 | 36 |
| Battleship | 1400 | 14 s | 800 | 45% | 90 | 38 | 5 | 45 |

- Ships appear in every faction's UNIT tab at the end of the roster.
- Completed ships spawn inside the lake and are clamped to its radius.
- Naval movement ignores land navigation, travels directly, stays at water level and
  never leaves the lake.
- Ships fight with the normal combat system; they separate only from other ships.

## Faction building colours
- Building materials are re-tinted at match start using the selected factions' accent
  colours (rust / green / teal), so bases now read as faction-owned, not just the units.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 65/65, PlayMode 60/60.

### Covered by tests

- Roster sizes include the naval ships.
- A Patrol Boat ordered out of the lake remains inside the lake radius and at water level.

## Known limits

- One fixed lake; no shore building placement or amphibious units.
- Ships are teleported from the Naval Yard into the lake rather than sailing out of a dock.
- No naval pathfinding, torpedoes, naval abilities or AI naval production.
- Ground units cannot enter water; ships cannot leave it.
- Navy was originally out of v1 scope; this is a functional first pass.
