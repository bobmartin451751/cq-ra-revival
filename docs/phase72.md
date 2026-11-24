# Phase 72 - faction superweapons

## What changed
- New `Superweapon` building (2000 credits, 150 power, 1000 HP, 8 m footprint).
- Each faction owns one distinct superweapon:
  - **Ashfall** - Ashfall Barrage: 6 artillery pulses, 120 damage, 6 m radius.
  - **Verdant** - Spore Bloom: 8 toxin pulses, 70 damage, 9 m radius.
  - **Meridian** - Orbital Lance: one 520-damage strike, 5 m radius.
- Every superweapon charges for 150 s, then fires on a clicked point.
- Selected superweapon shows its name, charge or READY state, and a STRIKE button.
- Strikes damage enemies only, respect alliances, hurt neutral bridges and count kills.
- Charge is saved and restored with the match snapshot.
- New procedural silo mesh with a faction trim, plus tracers for the launch.

## Verification
- EditMode **99/99**
- PlayMode **84/84**
- Windows player built.

New coverage:
- faction-to-superweapon mapping and distinct names/stats
- build-menu inclusion and cost/power sanity
- charged fire damages an enemy, resets charge and rejects a second shot

## Known limits
- The AI does not build or fire superweapons yet.
- No warning siren or minimap icon when a superweapon is charging or fired.
- Strike effects use tracer visuals only, with no ground scorch or spore cloud mesh.
