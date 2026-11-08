# Phase 18 - directional armour (spec extras, part 3)

## What exists

| Piece | Files |
|---|---|
| Angle-based damage math | `CombatMath.DirectionalMultiplier` in `CombatComponents.cs` |
| Unit combat application | `CombatSystem.cs` |
| Turret combat application | `DefenseSystem.cs` |
| Tests | `Assets/Tests/EditMode/Phase18Tests.cs` |

## Rules

Damage is scaled by the target's facing relative to the incoming shot:

| Arc | Condition | Multiplier |
|---|---|---:|
| Front | incoming direction within 60° of the target's forward | 75% |
| Side | neither front nor rear | 85% |
| Rear | incoming direction within 60° of the target's back | 100% |

The target's `LocalTransform.Rotation` defines its forward (+Z). Ground units turn toward
their travel direction, so flanking and rear attacks are rewarded. Buildings face their
spawn yaw (normally identity).

The multiplier is applied before the existing flat armour reduction:

```
damage = weaponDamage * directionalMultiplier * (1 - armour)
```

It applies to both unit combat and gun-tower fire. Air shots use the same XZ facing
calculation.

## Verification

```powershell
.\tools\run-tests.ps1
```

Suites: EditMode 56/56, PlayMode 49/49.

### Covered by tests

- Front, side and rear shots against an identity-facing target.
- A target turned 180° swaps front and rear arcs.
- Effective damage scales by the directional multiplier.

## Known limits (deliberate for Phase 18)

- One global set of arc multipliers; no per-unit rear/side values yet.
- Only facing is considered; no elevation or turret orientation.
- Buildings use their spawn yaw (usually forward).
- Air units use the same arcs as ground units.
