# Phase 19 - factions in missions

## What exists

| Piece | Files |
|---|---|
| Faction catalog, modifiers, runtime team mapping | `Assets/Scripts/Sim/Factions/FactionCatalog.cs` |
| Unit/building stat application | `UnitFactory.cs`, `BuildingFactory.cs` |
| Faction-scaled costs | `ProductionSystem.cs`, `BaseCommands.cs` |
| Skirmish faction picker | `SkirmishController.cs` |
| Mission faction assignment | `missions.json`, `MissionCatalog.cs`, `SkirmishController.cs` |
| Tests | `Phase19Tests.cs`, `FactionPlayModeTests.cs` |

## The three factions

| Faction | Identity | Cost | Health | Damage | Speed | Vision |
|---|---|---:|---:|---:|---:|---:|
| **Ashfall Compact** | balanced industrial baseline | 100% | 100% | 100% | 100% | 100% |
| **Verdant Combine** | fast, cheap, brittle | 90% | 92% | 95% | 110% | 98% |
| **Meridian Syndicate** | expensive, hard-hitting, long-sighted | 112% | 102% | 112% | 95% | 115% |

Modifiers apply to **any team using that faction** - the AI gets exactly the same
bonuses and penalties as the player. Costs scale unit production and building placement.

## How factions flow into matches

- **Skirmish:** choose one of the three factions in the menu; the AI is assigned the
  default counter-faction, and the menu shows the matchup.
- **Campaign:** each mission declares `playerFaction` and `aiFaction`; the briefing and
  objective HUD show the matchup.
- Starting armies now spawn **after** the match starts, so the chosen factions are applied
  to the opening units, not just later production.
- The content pipeline already supports per-kind overrides; faction-specific rosters can
  replace the shared roster in a later phase.

Mission assignments:

| Mission | Player | AI |
|---|---|---|
| First Contact | Ashfall | Verdant |
| Hold the Line | Meridian | Verdant |
| Break Their Base | Ashfall | Meridian |

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 59/59, PlayMode 50/50.

### Covered by tests

- Three distinct factions exists with different modifiers.
- Team-to-faction mapping and faction-scaled costs round correctly.
- Default opponents are always a different faction.
- Spawned units receive faction health, damage, speed and vision modifiers.

## Known limits (deliberate for Phase 19)

- All factions still share the same models, meshes and unit/building roster.
- No faction-unique units, buildings, abilities or superweapons yet.
- No faction colour palette or insignia yet.
- Skirmish AI faction is the default opponent rather than a separate picker.
- Campaign uses the same map for all three missions.
