# Phase 63 - save restore: ore, crates and research

## What changed
Extended the save/load restore pipeline:

- ore node positions, amounts and radii are saved and restored
- crate positions, rewards and kinds are saved and restored
- completed research masks are saved and restored per team
- existing ore/crates are cleared before restoring saved ones

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- Fog, AI commander state, garrison/cargo and hero progress still are not captured.
- Restored buildings do not re-block the nav grid yet.
- No save/load menu or save slots.
