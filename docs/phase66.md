# Phase 66 - save AI state and menu loading

## What changed
- AI commanders are saved: team and difficulty.
- Applying a snapshot clears the generated commanders and recreates the saved ones.
- Main menu now offers **LOAD SAVE** when a save file exists.
- Menu loading queues the snapshot and reloads the correct map.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- AI commander attack state and think timer are not saved.
- Fog state is still not saved; fog is regenerated on load.
- No save slots or save-version migration.
