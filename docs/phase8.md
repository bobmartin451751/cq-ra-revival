# Phase 8 - skirmish AI

## What exists

| System | Files |
|---|---|
| AI commander component and difficulty profiles | `Assets/Scripts/Sim/AI/AiComponents.cs` |
| Decision system (deploy, build, produce, attack) | `Assets/Scripts/Sim/AI/AiSystem.cs` |
| Deterministic AI building placement | `Assets/Scripts/Sim/Base/BaseCommands.cs` (`TryAutoPlace`) |
| AI capture mode | `Assets/Scripts/Core/ScreenshotCapture.cs`, `tools/screenshot.ps1` |
| Tests | `Assets/Tests/EditMode/Phase8Tests.cs`, `Assets/Tests/PlayMode/SkirmishAiPlayModeTests.cs` |

The AI is one `AiCommander` entity per controlled team. It runs in the fixed-step
simulation, so its decisions are part of the deterministic replay.

## The hook: same rules, no bonuses

`AiSystem` never writes simulation state directly. It uses exactly the player-facing
commands:

- `BaseCommands.TryDeployMcv` for the starting MCV.
- `BaseCommands.TryAutoPlace` / `TryPlaceBuilding` for structures, paying normal costs
  and respecting the 45 m build radius, terrain, overlap and credits.
- `ProductionCommands.TryEnqueue` for every unit, paying normal costs and using the
  same 5-item queues and build times.
- `CombatState` attack orders with explicit targets, the same mechanism as right-click.
- Harvesters run the normal autonomous ore loop.

There are no credit multipliers, free units, stat changes or construction discounts.
Difficulty changes decision quality only.

## Difficulty profiles

| Tier | Think | Attacks at | Army cap | Tanks | Harvesters | Barracks | Factories | Refineries | Power | Turrets |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Recruit | 2.00 s | 8 | 8 | 25% | 2 | 1 | 1 | 1 | 1 | 0 |
| Veteran | 1.50 s | 6 | 11 | 40% | 3 | 1 | 1 | 2 | 2 | 1 |
| Hardened | 1.00 s | 5 | 14 | 55% | 3 | 2 | 2 | 2 | 2 | 2 |
| Brutal | 0.75 s | 4 | 18 | 70% | 4 | 2 | 2 | 3 | 3 | 2 |

Higher tiers think more often, commit smaller armies, field more armour and expand
production sooner.

## Decision loop

Every think interval, per commander:

1. **Deploy.** No construction yard: deploy the MCV; if there is no MCV, queue one
   through the war factory path (or wait for credits).
2. **Production first.** Keep harvesters at the difficulty target, then queue army
   units toward the cap using the tank ratio. This prevents the AI from spending every
   think on construction while its barracks sit idle.
3. **Construction.** One building per think, in priority order: power shortage, refinery,
   first barracks, first war factory, first gun tower, extra barracks, extra war factory,
   extra power.
4. **Military.** Gather armed infantry and tanks. Once the army reaches the difficulty
   threshold, order every idle unit to attack the nearest enemy building (falling back
   to units). Units with no target or order are re-tasked each think.

The AI is deterministic: no random numbers, stable entity-order tie breaking.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase8-ai.png -Ai -AiLevel 3 -WaitFrames 6000
```

`-Ai` creates commanders for both teams and lets them play from their starting MCVs;
`-AiLevel` selects the difficulty (0 Recruit, 1 Veteran, 2 Hardened, 3 Brutal).

Suites: EditMode 36/36, PlayMode 33/33.

### Covered by tests

- AI profiles are positive and harder tiers think faster with smaller attack armies.
- An AI team deploys its MCV, adds structures and spends its own credits.
- The AI queues or completes units through normal production.
- A gathered army above the threshold receives explicit attack orders on an enemy base.

## Known limits (deliberate for Phase 8)

- The AI currently sees the whole map because fog of war does not exist yet. When vision
  lands, the AI's target selection must be restricted to visible enemies (no fog cheat).
- No scouting, retreat micro, retaliation response or defensive structures.
- No UI/skirmish setup yet; the scene is wired to the AI in the skirmish phase.
- The capture helper is a demo path, not a menu option.
