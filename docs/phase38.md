# Phase 38 - MCV-only start, map previews and base validation

## Starting forces
Matches now start with **one MCV per team and nothing else**.

- No free harvester.
- No free tanks.
- No free infantry.

Production changed accordingly:

| Unit | Producer |
|---|---|
| Harvester | **Refinery** |
| Infantry / Engineer | Barracks |
| Tank / MCV / Gunship / uniques | War Factory |
| Patrol Boat / Battleship | Dock |

Refineries now own a production queue as well as their ore drop-off role.

## Base placement validation
Added a data-level validation over all 50 maps and every spawn:

- start is on land
- enough free build sites around each spawn
- no build site inside water or a ruin
- every water body has at least one valid dock site
- every dock launch point is inside that body's water

This does not replace a human playthrough, but it catches maps where an AI
commander would have nowhere to deploy a yard or refinery.

## Map previews
The skirmish map browser now shows a generated top-down preview card:

- terrain-like background
- water bodies
- ruins
- ore patches
- team-coloured spawn dots

The campaign mission screen also shows the mission map preview.

## Terrain edges
- Terrain resolution now scales with map size and is higher on large maps.
- Shoreline blending widened from 10 m to 12 m.
- Generated map basin blends widened to 9% of map size.

## Multi-lake ship launching
Added a PlayMode test that builds a dock on the **second lake** of Twin Shallows,
queues a ship there, and verifies the ship launches into that body rather than
the first lake.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **83/83**
- PlayMode **69/69**
- Windows player built.

## Known limits
- Map previews are stylised data cards, not exact rendered terrain.
- Terrain edges are smoother but still heightmap-based.
- Base placement validation checks geometry, not a full AI economy simulation on
  every map.
- The 45 generated family maps remain variants rather than fully bespoke layouts.
