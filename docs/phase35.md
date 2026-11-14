# Phase 35 - selectable skirmish maps

## What changed
Skirmish now has **three deterministic maps**, selectable in the start menu:

| Map | Shape | Starts |
|---|---|---|
| **Ash Basin** | The original open basin and ruin layout | South vs north |
| **Rusted Flats** | Low, wide ground with long sight lines | Southwest vs northeast |
| **Broken Ridge** | Tight basin, taller ridges and short lanes | Southwest vs northeast |

Each map controls:
- terrain seed, maximum height, noise scale and basin shape
- ruined-outcrop positions, sizes and seeds
- ore patch and crate layouts
- player and AI starting positions and facing

The western lake remains at the same site on every map so naval rules and ship
spawning stay consistent.

### Runtime flow
- `MapRuntime` stores the selected map across scene reloads.
- Selecting a different map and pressing **START MATCH** reloads the scene with
  the pending difficulty/faction choices preserved.
- Campaign missions always use **Ash Basin**; starting a mission from another map
  reloads into the campaign map.
- Restarting a match keeps the current map.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **74/74**
- PlayMode **63/63**
- Windows player built.

### New tests
- Three maps exist with unique ids and seeds.
- Both starts are separated and inside each map's flat basin.
- Every map has scenery, ore and crate data within the play area.
- `MapRuntime` selection clamps out-of-range indices.

## Known limits
- The lake position, water level and naval geometry are still shared by all maps.
- Maps are code-authored presets, not external JSON files or a map editor.
- No per-map minimap preview or 8-player/team layouts yet.
- Campaign missions reuse Ash Basin.
