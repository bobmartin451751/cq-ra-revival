# Phase 33 - faction base silhouettes

## What changed
- Buildings now render a **faction-specific mesh** instead of one shared silhouette.
  The shared base geometry is preserved (so footprints and recognition stay stable),
  with a distinct roof module added per faction:
  - **Ashfall Compact** - twin exhaust stacks and a pipe gantry. Industrial, blunt.
  - **Verdant Combine** - low roof canopy, solar panel and a round water tank.
  - **Meridian Syndicate** - tall antenna mast, crossbar and an angled fin.
- Every building kind gets all three variants, including walls and the Naval Yard.
- `BuildingFactory` now indexes meshes as `faction * BuildingKindCount + kind`.
  Material tinting still follows the team/faction colour pass from Phase 29.
- Ore, crate and mine meshes moved after the 24 building variants.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **68/68**
- PlayMode **63/63**
- Windows player built.

### New tests
- The three Construction Yard variants have distinct vertex counts.
- All 8 building kinds have a non-empty mesh for all 3 factions.
- The render mesh index follows the team's assigned faction.

## Known limits
- Variants use the same material and only differentiate silhouette, not texture.
- Added modules are decorative; collision, vision and build footprints are unchanged.
- No faction-unique building types or abilities yet.
