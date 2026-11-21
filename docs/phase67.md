# Phase 67 - save AI timers and version gate

## What changed
- AI commander snapshots now include think timer and attacking state.
- Restoring a save recreates commanders with those values.
- Save files with a mismatched version are rejected instead of partially loaded.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- Fog state is still regenerated rather than restored.
- No save slots or migration between versions.
