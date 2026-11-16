# Phase 55 - order acknowledgements and attack ping

## What changed
- Move orders now show a short `Moving out` acknowledgement.
- Attack orders show `Engaging`.
- Base-under-attack alerts now record the attack position.
- The minimap shows a red ping at the latest attack position for 8 seconds.

Existing radio cues still play with these messages.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Acknowledgements are text/radio cues, not voiced lines.
- No directional arrow or off-screen indicator.
- Only the latest attack position is shown.
