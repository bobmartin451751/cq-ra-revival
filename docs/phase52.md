# Phase 52 - dynamic combat music intensity

## What changed
Explosions now feed the combat music layer:

- each explosion raises combat intensity
- percussion volume increases
- percussion pitch rises slightly
- intensity decays when fighting stops

Ambient drone and other cues are unchanged.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Intensity is driven by explosions only, not all combat.
- No layered stems or transitions.
- Volume is still fixed relative to the master audio level.
