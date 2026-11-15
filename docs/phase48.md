# Phase 48 - procedural ambience

## What changed
Added a looping synthesized ambient bed to the existing procedural audio layer:

- 12-second seamless low drone
- generated at scene start, no audio assets
- independent ambient source at low volume
- stops and cleans up when the audio director is destroyed

This gives battles a continuous industrial background instead of silence between
UI and combat cues.

## Verification

- EditMode **90/90**
- PlayMode **77/77**
- Windows player built.

## Known limits
- No layered music states for menu/build/combat.
- No announcer or unit voice acknowledgements.
- Ambient volume is fixed relative to the master cue volume.
