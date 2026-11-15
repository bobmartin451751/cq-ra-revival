# Phase 50 - persistent hero veterancy

## What changed
Campaign hero kills now persist between missions per faction using PlayerPrefs:

- hero starts with its saved kill count
- veterancy ranks are reapplied from that count
- kills are saved when a mission ends
- mission briefing shows the saved kills

Each faction has its own hero progress key.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Only kills persist; health, position and equipment do not.
- If the hero dies before mission end, the latest kills may not be saved.
- No hero ability or branching progression.
- Clear via deleting the faction's `cq_hero_*_kills` PlayerPrefs key.
