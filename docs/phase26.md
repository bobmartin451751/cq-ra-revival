# Phase 26 - veterancy

## What exists

| Piece | Files |
|---|---|
| Veterancy component and rank thresholds | `Assets/Scripts/Sim/Units/UnitComponents.cs` |
| Rank-up system | `Assets/Scripts/Sim/Units/VeterancySystem.cs` |
| Kill credit | `CombatSystem.Fire` |
| Spawn assignment | `UnitFactory.cs` |
| Sidebar rank display | `SelectionController.cs` |
| Tests | `Phase26Tests.cs`, `VeterancyPlayModeTests.cs` |

## Rules

Armed units start at rank 0 and earn kills.

| Rank | Kills | Bonus |
|---|---:|---|
| 0 | 0-1 | none |
| 1 | 2-4 | +15% damage, +10% max health |
| 2 | 5-8 | +30% damage, +21% max health |
| 3 | 9+ | +52% damage, +33% max health |

Bonuses are applied at the moment the rank is earned. New maximum health is granted
immediately, so a rank-up also heals the difference. Unarmed units (harvester, MCV,
engineer) do not gain veterancy.

The selected-unit panel shows `Rank N` next to the unit name.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 65/65, PlayMode 57/57.

### Covered by tests

- Rank thresholds at 2, 5 and 9 kills.
- A unit at 2 kills ranks up to rank 1 with +15% damage and +10% max health, and the new
  health is granted immediately.

## Known limits

- No chevron/star art; the sidebar uses text.
- Veterancy is not shown for unselected units.
- No veterancy decay, kill sharing or support-unit experience.
- Bonuses are global constants, not in the content table yet.
