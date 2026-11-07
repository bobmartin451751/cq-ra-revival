# Phase 4 - combat core

## What exists

| System | Files |
|---|---|
| Hit points, weapon, engagement and match state | `Assets/Scripts/Sim/Units/CombatComponents.cs` |
| Acquisition, firing, damage, death, match resolution | `Assets/Scripts/Sim/Units/CombatSystem.cs` |
| Presentation tracer queue | `Assets/Scripts/Sim/Units/CombatFeed.cs` |
| Tracer renderer | `Assets/Scripts/View/CombatTracerRenderer.cs` |
| Right-click attack orders + HUD | `Assets/Scripts/View/SelectionController.cs` |
| Capture helper for combat shots | `Assets/Scripts/Core/ScreenshotCapture.cs` |

Combat runs in `FixedStepSimulationSystemGroup` after movement, so it shares the
30 Hz deterministic clock with the rest of the simulation. Hitscan only: no physics,
no projectile entities, no frame delta time and no randomness. An engagement with the
same inputs replays identically.

## Unit combat stats

Tuning lives in `CombatStats.For(UnitKind)`.

| Kind | HP | Armour | Damage | Range | Cooldown | Acquire |
|---|---:|---:|---:|---:|---:|---:|
| Infantry | 50 | 0% | 5 | 13 m | 0.45 s | 16 m |
| Tank | 260 | 35% | 45 | 24 m | 1.6 s | 28 m |
| Harvester | 220 | 25% | - (unarmed) | - | - | - |

Damage after armour is `damage * (1 - clamp(armour, 0, 0.9))`, so a tank takes
29.25 damage from another tank and a tank one-shots infantry. Armour is clamped so
no unit can ever become invulnerable.

## Engagement rules

- **Auto acquisition.** An armed unit with no move order picks the nearest enemy
  inside its acquisition radius. Ties break on entity index, keeping targeting
  deterministic. It fires while the target stays inside `range * 1.15`, then drops it.
- **Attack order.** Right-clicking an enemy issues an explicit order to every selected
  armed unit. Explicit targets are never dropped for distance: a unit outside weapon
  range walks into range (a normal move order with the target position as the flow
  field destination), then fires. The order clears when the target dies.
- **No friendly fire.** Targets are filtered by `UnitInfo.Team`; a unit can never
  acquire or damage a teammate.
- **Death.** Entities at zero health are destroyed at the end of the tick. Field-cache
  references to dead units are simply pruned by the flow-field system.
- **Match state.** `MatchState` holds live unit counts per team, `GameOver`, and the
  winning team (255 = draw / in progress). The HUD shows team counts and draws a
  winner banner. Counts are recomputed every tick, so a future restart needs no
  explicit reset.

## Presentation

`CombatSystem` writes muzzle-to-target segments to `CombatFeed`, a bounded managed
queue. `CombatTracerRenderer` drains it and draws short-lived line renderers, fades
them over 0.16 s and keeps a pool of 64. The feed is presentation-only; the view never
writes simulation state, so headless runs and tests are unaffected.

The selection HUD now reports `R:<alive> B:<alive>` for the two teams, the health of
the first selected unit, and a RED TEAM WINS / BLUE TEAM WINS / DRAW banner when the
match resolves.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase4-battle.png -Attack -MoveTeam 0 -WaitFrames 800
```

`-attack` orders every armed unit on a team to attack its nearest enemy, using the same
`CombatState` an ordinary right-click writes. The capture waits 800 frames, which is
roughly when the two spawn groups meet at the 30 Hz tick rate.

Suites: EditMode 23/23, PlayMode 15/15.

### Covered by tests

- All kinds have positive health and sane armour; the harvester is unarmed.
- Armour math clamps and never produces negative damage.
- Shots-to-kill matches the armour model (tank vs infantry, tank vs tank).
- Tanks outrange, out-health and out-armour infantry.
- An idle tank auto-acquires and destroys an infantryman in range.
- Friendly units never acquire or damage each other.
- An explicit attack order chases a target 80 m away and kills it.
- Killing the last enemy unit sets `MatchState.GameOver` and the winning team.
- An unarmed harvester is still a valid (destructible) target.

## Known limits (deliberate for Phase 4)

- No projectiles, splash damage, accuracy, veterancy or directional armour yet.
- No attack-move or guard modes; units auto-engage only while idle.
- Target search is a deterministic O(n^2) scan. Fine for a few hundred Phase 4 units;
  it moves into a spatial-hash Burst job in the performance pass.
- No death animation or wrecks; units are removed on the tick they die.
