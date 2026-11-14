# Phase 36 - creative water maps and shoreline docks

## Map water
Every skirmish map now owns its water:

| Map | Water |
|---|---|
| **Ash Basin** | One circular western lake |
| **Rusted Flats** | Dry — no water, no naval play |
| **Broken Ridge** | Large polygonal central lake |
| **Twin Shallows** | Two separate circular lakes |
| **Flooded Delta** | Large irregular western delta polygon |

- Water bodies are circles **or simple polygons**.
- Terrain carving, the nav grid, water rendering and ship movement all use the
  same water data.
- Polygons are triangulated for rendering; ships are clamped inside and pushed
  away from the shoreline.
- Multiple lakes are separate bodies. A ship stays in the body its dock belongs
  to and cannot cross to another lake.

## Dock
The old Naval Yard is now the **Dock**:

- Built on land close to a water body; its placement records that body and a
  launch point inside the water.
- Produces Patrol Boats and Battleships through the normal queue.
- Completed ships spawn at the dock's water outlet, at the correct water level,
  and remain in that water body.
- The dock is hidden and rejected on dry maps.
- The AI builds a dock only on maps with water and skips naval production on
  **Rusted Flats**.

## Rendering and navigation
- `WaterRenderer` builds one translucent mesh per water body.
- `HeightmapTerrain` carves every water body with a shoreline blend.
- `NavGridBuilder` accepts an extra blocker callback so polygon and circle water
  both block ground pathing.
- `MapWater` provides containment, clamping, shoreline distance and dock-site
  search.

## Development helper
Launching with `-map N` selects a skirmish map before the scene generates:

```powershell
.\Builds\Windows\CQRaRevival.exe -map 2
```

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **77/77**
- PlayMode **65/65**
- Windows player built.

### New tests
- Map water counts, shapes and start/resource placement.
- Polygon and circle nav blocking.
- Ship clamping inside circle and polygon water.
- Dock placement near water and rejection on dry maps.
- AI finds a shoreline dock and a dry map rejects it.
- Dock production launches a ship inside the dock's water body.

## Known limits
- Polygons are simple outlines; holes/islands are not supported yet.
- Separate lakes are not connected for ship travel.
- Water is flat at a per-body level and has no waves or shoreline foam.
- Ships cannot transfer between lakes or sail to a dock on another body.
