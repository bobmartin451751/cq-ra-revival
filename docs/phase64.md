# Phase 64 - save restore: navigation blocking

## What changed
Restored buildings now re-block the nav grid using their normal nav radius.

This prevents saved bases from appearing visually while units path through their
footprints.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- Flow fields are not explicitly invalidated after restore.
- Unit evacuation from restored footprints is not run.
- Fog, AI state, cargo and save-slot UI remain pending.
