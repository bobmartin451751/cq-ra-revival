# Phase 53 - radio announcer subtitles

## What changed
Added short radio-style announcer subtitles for match events:

- `Battle control online` at match start
- `Victory`, `Defeat` or `Battle ended in a draw` at match end
- `Mission complete` / `Mission failed` for campaign missions

Subtitles appear at the top centre for a few seconds and play a short radio-style
click cue. They clear when returning to the main menu.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Text only; no synthesized voice.
- No unit acknowledgements or base-under-attack warnings yet.
- No queueing of multiple messages.
