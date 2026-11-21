# Phase 65 - save restore: cargo and flow fields

## What changed
- Transport/garrison cargo is included in unit snapshots.
- Cargo kind and health are restored after the unit respawns.
- Flow fields are invalidated after restoring a match.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- Fog and AI commander state still are not saved.
- No save/load menu or slots.
- Cargo visual/UI state is not restored until selection.
