# Phase 40 - garrison bunkers

## Bunker
New **Bunker** building:

- cost 500
- no power draw
- HP 650
- land placement
- build hotkey **0**
- 5 infantry capacity

Bunker behaviour:

- right-click with selected infantry to garrison
- occupants are removed from the field while inside
- the bunker gains firepower per occupant
- base damage 6, plus 7 per garrisoned soldier
- press **E** with the bunker selected to eject occupants
- the selection panel shows `Garrison: n/5`

Only infantry, engineers and Mantis-type infantry can enter.

## Visuals
Bunker has a faction-variant mesh, faction colour tint and build-row entry.

## AI/air follow-up
- AI aircraft production now cycles through fighter, bomber and attack-helicopter
  roles.
- AI transports remain disabled.
- The existing AI attack test was stabilised against shared test-world fog and
  leftover entities.

## Verification

- EditMode **87/87**
- PlayMode **74/74**
- Windows player built.

## Known limits
- Bunkers cannot be captured while occupied.
- No separate interior UI or per-occupant weapon types.
- AI does not build or use bunkers yet.
- Odd-player team balancing remains intentionally unfixed.
