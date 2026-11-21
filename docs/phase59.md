# Phase 59 - spectator speed controls

## What changed
AI-vs-AI observer mode now supports:

- `[` and `]` to cycle simulation speed between 1x, 2x and 4x
- `P` to pause/resume
- on-screen spectator speed/pause label

Speed and pause controls only apply in observer mode and reset when a match starts.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Fast-forward changes Unity time scale rather than adding a deterministic tick scheduler.
- No replay-safe speed control yet.
- No slow-motion or step-frame controls.
