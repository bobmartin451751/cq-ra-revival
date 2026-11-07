# Phase 7 - building combat, repair and sell

## What exists

| System | Files |
|---|---|
| Unified unit/building targeting and damage | `Assets/Scripts/Sim/Units/CombatSystem.cs` |
| Building removal + nav unblocking | `Assets/Scripts/Sim/Base/BaseCommands.cs` |
| Repair state, repair/sell rules | `Assets/Scripts/Sim/Base/BaseComponents.cs` |
| Continuous repair system | `Assets/Scripts/Sim/Base/RepairSystem.cs` |
| Attack/repair/sell input + health bars | `Assets/Scripts/View/SelectionController.cs` |
| Nav circle freeing | `Assets/Scripts/Sim/Navigation/NavGridQuery.cs` |

Everything remains in the 30 Hz fixed-step simulation. Repair is deterministic: the
same toggles and credits produce the same health curve.

## Building combat

- Units can acquire and attack **buildings** as well as units. The target query accepts
  anything with `Health` and either `UnitInfo` or `BuildingInfo`.
- Auto-acquisition, explicit attack orders, chasing and the stop-at-weapon-range rule
  all work unchanged against buildings.
- Right-clicking an enemy **unit or building** with units selected issues an attack
  order, exactly like right-clicking a unit.
- Only the player's own units and buildings are selectable; enemies are never added to
  the selection (they remain attack targets).
- Buildings use the same armour model as units (50% on all buildings) and their own HP:

| Building | HP |
|---|---:|
| Construction Yard | 1200 |
| Refinery | 900 |
| War Factory | 800 |
| Power Plant | 600 |
| Barracks | 500 |

- On destruction the building is removed, its nav footprint is freed, and cached flow
  fields are invalidated so units can path through the rubble.
- `MatchState` now counts **units and buildings** per team, so a team with only
  buildings remains alive (and can rebuild), and a team is eliminated only when both
  are gone.
- Damaged units and buildings show a colour-coded health bar above them (red to green).

## Repair

- Select a building and press **R** to toggle repair on/off.
- Repair heals at **40 HP/s** while costing **30 credits/s**, paid per tick.
- Repair stops automatically when the building is full or the team cannot pay.
- Cancelling repair is free; starting requires damage and an economy.

## Sell

- Select a building and press **T** to sell it.
- The refund is **50% of the original cost** (yard excluded):

| Building | Refund |
|---|---:|
| Barracks | 200 |
| Power Plant | 150 |
| Refinery | 300 |
| War Factory | 450 |

- Selling frees the nav footprint, removes the building and drops its production queue.
- The Construction Yard cannot be sold (it defines the build radius).

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase7-siege.png -Siege -WaitFrames 900
```

`-Siege` builds both bases and armies, then orders every armed red unit to attack the
nearest blue building. At 900 frames the assault is engaged and tracers are visible.

Suites: EditMode 34/34, PlayMode 30/30.

### Covered by tests

- Sell refunds are half cost and the yard is not sellable.
- Repair rates are positive and consistent with the cost model.
- A tank destroys an enemy power plant, the nav footprint becomes walkable, and the
  match resolves to the attacker.
- Repair heals over time, spends credits, toggles off, and selling then refunds half.
- Repair stops immediately when the team has no credits.
- Units never acquire or damage friendly buildings.
- Selection rules reject enemy units and buildings while accepting the player's own.

## Known limits (deliberate for Phase 7)

- Buildings cannot shoot back yet (no defensive structures).
- No construction/repair animations, damage textures or rubble models.
- Destroyed buildings are removed; they leave no wreck entity.
- Repair is per-building and has no repair depot/range restrictions.
- Selling is immediate, with no confirmation step.
