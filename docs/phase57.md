# Phase 57 - off-screen attack indicator

## What changed
When a base attack position is outside the current camera view, a red `!` marker
is clamped to the screen edge in the direction of the attack.

- Marker appears alongside the existing `Base under attack` subtitle.
- On-screen attacks rely on the minimap ping instead.
- Marker expires with the 8-second attack-position timer.
- Does not draw over the sidebar.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Marker is a text glyph, not a directional arrow.
- Only the latest attack position is tracked.
- No camera jump or ping sound specific to the indicator.
