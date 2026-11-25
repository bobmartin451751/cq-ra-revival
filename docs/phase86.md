# Phase 86 - attack-move

## What changed
- `MoveTarget` now carries an `AttackMove` flag.
- **A** arms attack-move in the sidebar; right-click issues the order.
- Attack-moving units keep their move order but still auto-acquire enemies along the way.
- When a target enters weapon range the unit stops to engage, matching normal RTS behaviour.
- A plain right-click move clears the flag.
- Move-order API remains source-compatible (`attackMove` is an optional parameter).
- Build panel hint now lists **A Attack-move**.

## Verification
- EditMode **116/116**
- PlayMode **94/94**
- Windows player built.

New coverage:
- attack-move sets both HasOrder and AttackMove
- the combat system acquires a visible enemy while the unit is still moving
- a normal move clears the flag

## Known limits
- Attack-move does not choose a formation facing or path of least resistance.
- Units still stop at the first acquired target rather than re-routing around it.
