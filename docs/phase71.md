# Phase 71 - infantry terrain cover

## What changed
- Added `CoverRules`: deterministic positional cover for infantry only.
- Cover is derived from the simulation terrain buffer:
  - a ridge or rise on the line from the infantry unit to the attacker
  - a depression around the unit
- Standing beside a ruined outcrop also grants cover.
- Damage multipliers: 0.70 for a heavy ridge, 0.85 for light rise, 0.78 near an outcrop.
- Vehicles, aircraft, ships and buildings never receive this bonus.
- Cover is applied after armour and before shields, and both sides use the same rules.

## Verification
- EditMode **96/96**
- PlayMode **83/83**
- Windows player built.

New coverage:
- a ridge between target and attacker lowers damage while flat ground does not
- non-infantry units always receive a 1x multiplier

## Known limits
- Cover is not shown in the HUD or on the ground.
- Garrisoned infantry rely on the building, not field cover.
- The AI does not deliberately reposition infantry into cover yet.
