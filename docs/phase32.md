# Phase 32 - terrain line of sight

## What changed
- Fog vision is no longer a plain circle. Each cell is now checked against a
  terrain sight line from the observing unit or building.
- `TerrainLineOfSight` marches the height field every **1.5 m** between the
  observer's eye and the target cell. Ground rising more than **0.35 m** above
  the sight line blocks the cell.
- Observer eye heights:
  - units: **2.2 m**
  - buildings: **8 m**
- Air units and naval units keep their full circular vision, because they sit
  above the terrain or are deliberately kept effective across the lake.
- Combat auto-acquisition and the AI both read the same fog grid, so neither
  side can see or auto-target through a ridge. Explicit player attack orders can
  still push into fog, as before.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **65/65**
- PlayMode **63/63**
- Windows player built.

### New test
- A 25 m ridge blocks fog behind it while the ground in front stays visible.

## Known limits
- LOS is sampled on the 4 m fog grid; very narrow terrain spikes can be missed.
- Buildings always use an 8 m eye height regardless of shape.
- Air and naval vision intentionally ignore terrain blocking.
- Explicit attack orders still ignore fog and terrain LOS.
