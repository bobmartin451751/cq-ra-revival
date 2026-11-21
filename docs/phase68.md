# Phase 68 - save/load fog of war

## What changed
- Each team's fog grid is saved with the match snapshot.
- Only the explored mask is persisted; transient visibility decays to explored.
- Loading restores explored cells and bumps the fog revision so the renderer refreshes.
- Grids whose dimensions no longer match are skipped safely.

This closes the last major save/load state gap: matches now resume with the same
explored map instead of a fresh fog grid.

## Verification
- EditMode **93/93**
- PlayMode **80/80**
- Windows player built.

New coverage:
- visible cells serialise as explored
- explored mask survives apply/restore
- fog revision is bumped on restore

## Known limits
- Save slots and cross-version migration are not implemented yet.
- Only one save file (`persistentDataPath/cqra-save.json`) is used.
