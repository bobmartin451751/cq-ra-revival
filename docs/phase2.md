# Phase 2 — terrain, camera and selection

## What exists

| System | Files |
|---|---|
| Procedural heightmap terrain | `Assets/Scripts/View/HeightmapTerrain.cs` |
| Procedural unit meshes | `Assets/Scripts/View/ProceduralMeshes.cs` |
| RTS camera rig | `Assets/Scripts/View/RtsCameraRig.cs` |
| Screen-space click / box selection | `Assets/Scripts/View/SelectionController.cs` |
| Runtime ECS unit spawning | `Assets/Scripts/Sim/Units/UnitSpawner.cs`, `UnitFactory.cs` |
| Selection highlight | `Assets/Scripts/Sim/Units/SelectionHighlightSystem.cs` |

The scene is still generated from code by `CQRa.EditorTools.SceneBuilder.CreateMainScene`.
No manual Editor work is required.

## Terrain

- 360 m × 360 m, 129 × 129 height samples (2.8 m spacing), fractal Perlin noise.
- A flattened basin in the centre keeps starting positions buildable; a gentle
  low-frequency undulation stops it looking like a perfectly flat disc.
- `SampleHeight(x, z)` does bilinear interpolation and is used by units, the camera
  and any future gameplay logic. No Unity Terrain component, no physics collider.
- A tiling procedural noise texture is generated at runtime for ground detail.

## Camera

| Input | Action |
|---|---|
| W A S D / arrow keys | pan relative to camera yaw |
| Middle-mouse drag | pan |
| Mouse wheel | zoom (20–140 m) |
| Q / E | rotate yaw |
| Screen edge | edge scroll (disabled when the window is not focused) |

The rig orbits a pivot that tracks the heightmap, clamps to the map bounds, and keeps
the camera itself above the surface. Motion is exponentially smoothed and framerate
independent.

## Selection

- Picking is screen-space: unit positions are projected with `WorldToScreenPoint` and
  compared against the cursor. No colliders, rays or physics.
- A drag creates a marquee; units inside the screen-space rectangle are selected.
- Shift adds to the selection; Shift-clicking an already-selected unit removes it.
- Selection is stored in an enableable `Selected` component, so no structural changes
  occur when selecting.
- `SelectionHighlightSystem` swaps selected entities onto a dedicated highlight material
  (pale gold + emission) and swaps them back on deselect.

## Units

`UnitSpawner` creates 22 units in two opposing groups: one harvester, four tanks and six
infantry per side. Units are real-world scale (infantry ~1.2 m, MBT 7 m, harvester 7 m).
Each unit is an ECS entity rendered through Entities Graphics with a shared
`RenderMeshArray`; team and kind map to distinct materials, and selection maps to the
highlight material.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase2.png
.\tools\screenshot.ps1 -Out .temp/screens/phase2-selected.png -SelectAll
```

Current suites: EditMode 8/8, PlayMode 3/3.
