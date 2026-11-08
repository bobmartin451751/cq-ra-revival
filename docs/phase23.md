# Phase 23 - Aegis shields

## What exists

| Piece | Files |
|---|---|
| Shield component | `Assets/Scripts/Sim/Units/UnitComponents.cs` |
| Damage absorption | `CombatMath.AbsorbWithShield` in `CombatComponents.cs` |
| Combat integration | `CombatSystem.cs`, `DefenseSystem.cs` |
| Regeneration | `Assets/Scripts/Sim/Units/ShieldSystem.cs` |
| Spawn assignment | `UnitFactory.cs` (Meridian Infantry) |
| Tests | `Assets/Tests/PlayMode/FactionPlayModeTests.cs` |

## Behaviour

Meridian **Aegis** infantry spawn with a **40-point energy shield**.

- Incoming damage is absorbed by the shield **before health**.
- Any hit resets the regeneration delay.
- After **4 seconds** without damage the shield regenerates at **12 points/second** up to 40.
- Overshooting damage carries through to health after the shield breaks.
- Both unit combat and gun-tower fire use the same absorption rule.

The shield is currently invisible; the unit's normal health bar remains the only readout.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 64/64, PlayMode 53/53.

### Covered by tests

- Aegis infantry spawn with a shield.
- Incoming damage is absorbed first and the remainder carries over.
- The shield regenerates after its quiet period.

## Known limits

- No shield bar, hit flash or bubble visual yet.
- Only Meridian Infantry have shields.
- Shield values are hardcoded at spawn; they are not in the content table yet.
- Remaining abilities: Mantis cloak/detection, Sower mines, Rig repair aura.
