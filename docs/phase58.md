# Phase 58 - AI vs AI observer mode

## What changed
Added an **OBSERVE AI VS AI** button to skirmish setup.

- Starts the selected map/player count/mode/difficulty.
- Creates an AI commander for every team, including team 0.
- Disables player selection input.
- Leaves camera controls active for observing.
- Uses the same fog, economy and AI rules as normal matches.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- No spectator-specific UI or speed controls.
- Returning to the menu resets observer mode.
- No AI-vision overlay yet.
