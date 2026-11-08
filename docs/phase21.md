# Phase 21 - faction colour palettes and air superiority

## What exists

| Piece | Files |
|---|---|
| Faction unit palettes | `UnitSpawner.ColorFor` |
| Skyfall anti-air bonus | `CombatMath.AirSuperiorityMultiplier`, `CombatSystem.Fire` |
| Tests | `Assets/Tests/EditMode/Phase21Tests.cs` |

## Palettes

Unit materials are rebuilt when the starting army spawns, after the match factions are
chosen. Team accents now follow the faction rather than a fixed red/blue:

| Faction | Accent |
|---|---|
| Ashfall Compact | rust red |
| Verdant Combine | green |
| Meridian Syndicate | teal blue |

Kind base colours are still blended in, so infantry, tanks, harvesters, aircraft and the
faction uniques stay readable. Buildings keep their existing team materials for now.

## Skyfall air superiority

The Meridian Skyfall deals **double damage to air units**, realised as:

```
damage = weaponDamage * directional * airSuperiority * (1 - armour)
```

It keeps normal damage against ground targets. Other gunships do not receive the bonus.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 64/64, PlayMode 51/51.

### Covered by tests

- Skyfall doubles damage against air and not against ground.
- Gunships never receive the air-superiority bonus.
- Faction rosters still map to the correct unique unit.

## Known limits

- Buildings do not yet use faction palettes.
- Base Infantry/Tank/Gunship still share silhouettes; only the three uniques have unique
  models.
- Remaining signature abilities (deploy artillery, cloak, shields, mines, repair aura)
  are next.
