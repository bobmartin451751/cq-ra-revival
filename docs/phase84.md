# Phase 84 - AI retreat and regroup

## What changed
- `AiCommander` now tracks a `RetreatTimer`.
- Before engaging, the AI checks its armed units' combined health.
- If the army falls below **30%** aggregate health while attacking:
  - every unit clears its combat target
  - the group receives a move order back to the Construction Yard
  - the commander stops attacking and enters a 20-second regroup window
- During the regroup window the AI keeps units at home instead of trickling them back
  into the fight.
- Retreat state is included in saves and replays via `SavedCommander.retreatTimer`.
- Behaviour is deterministic and uses the same movement/combat components as the player.

## Verification
- EditMode **114/114**
- PlayMode **93/93**
- Windows player built.

New coverage:
- full-health armies keep attacking
- armies under 30% health trigger retreat
- healed armies are ready to attack again

## Known limits
- Units do not repair themselves while regrouping; they only wait.
- Retreat always heads to the Construction Yard, not the nearest safe building.
- No tactical retreat around specific threats or chokepoints.
