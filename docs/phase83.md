# Phase 83 - rendered terrain map previews

## What changed
- Map preview cards are now rendered from the same terrain formula the game uses:
  - four octaves of Perlin noise at the map's seed, noise scale and max height
  - basin flattening and gentle undulation
  - water carved by the map's water bodies
- Relief shading comes from finite-difference normals, so ridges and valleys read clearly.
- Water uses shallow/deep depth colouring instead of one flat sea tone.
- Obstacles, ore, spawns and team dots are still drawn over the terrain.
- Preview textures stay CPU-readable, enabling tests and future texture export.
- Custom maps use the same rendered preview path.

## Verification
- EditMode **114/114**
- PlayMode **92/92**
- Windows player built.

New coverage:
- a preview is 128x128, renders visible relief variation and is cached
- a custom map produces a rendered preview

## Known limits
- Previews are top-down renders, not in-engine 3D screenshots.
- Water depth is approximated from height rather than simulated bathymetry.
- No labels, grid lines or elevation legend.
