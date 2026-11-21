# Phase 62 - save/load restore pipeline

## What changed
Added the first working load pipeline:

- `F5` saves the current match.
- `F9` loads the save, reloads the map scene and applies the snapshot.
- Save file: `persistentDataPath/cqra-save.json`.
- Restore rebuilds:
  - team credits
  - units with health and veterancy kills
  - buildings with health
- Map, factions, team count and mode are restored from the save.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

## Known limits
- Fog, research, AI state, ore/crate state, cargo and hero progress are not captured.
- Restored buildings do not re-block the nav grid; a full nav rebuild is still needed.
- Save/load is hotkey-only, with no menu UI.
- No save slot management or version migration.
