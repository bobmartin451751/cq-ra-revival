# Phase 74 - in-game map editor

## What changed
- Main menu has a third tab: **MAP EDITOR**.
- The editor exposes four deterministic settings:
  - seed (with +/- and RANDOMIZE)
  - size tier (Small/Medium/Large/Huge)
  - water on/off
  - player count (clamped to the tier maximum)
- A live preview is generated from the same map data the match will use.
- **SAVE** writes the four settings to `persistentDataPath/cqra-custom-map.json`.
- **PLAY CUSTOM MAP** deploys the generated map and starts a skirmish on it.
- `CustomMapFactory` builds terrain parameters, spawns, obstacles, ore and crates from
  the settings; the same values always regenerate the same map.
- `MapRuntime` can serve a custom map instead of a catalog entry until a normal map is
  selected again.
- Adding a third menu tab keeps all existing skirmish/campaign controls unchanged.

## Verification
- EditMode **105/105**
- PlayMode **84/84**
- Windows player built.

New coverage:
- custom-map generation is deterministic and seed-sensitive
- player count clamps to tier limits and water toggles correctly
- settings JSON round trip and regeneration
- custom map overrides the catalog until cleared

## Known limits
- The editor changes generation parameters; it does not hand-place individual props.
- Only one custom map slot is stored.
- Save files and replays that reference the custom map fall back to map 1 on load.
- No undo/history or in-editor minimap painting.
