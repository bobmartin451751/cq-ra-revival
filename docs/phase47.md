# Phase 47 - AI bunker garrison

## What changed
AI commanders now use Bunkers:

- build one Bunker once credits allow
- gather nearby friendly infantry
- load up to five soldiers into the Bunker
- Bunker defence fire scales with the garrison as before

The AI uses the same `GarrisonCommands` as the player, so it pays the same costs
and obeys the same five-slot limit.

## Verification

- EditMode **90/90**
- PlayMode **77/77**
- Windows player built.

## Known limits
- AI builds only one Bunker.
- AI does not eject or reposition garrisoned infantry.
- No AI-specific Bunker placement strategy.
