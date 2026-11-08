# Phase 20 - faction-unique troops

## What exists

| Piece | Files |
|---|---|
| Three unique unit kinds | `UnitComponents.cs` |
| Stats for the uniques | `ContentDatabase.cs`, `content.json` |
| Faction rosters and names | `ProductionCatalog` in `ProductionComponents.cs` |
| Unique meshes | `ProceduralMeshes.Thumper/Mantis/Skyfall` |
| Sidebar / hotkey roster | `SelectionController.cs` |
| AI unique production | `AiSystem.cs` |
| Tests | `Phase20Tests.cs`, faction PlayMode tests |

## Faction rosters

Every faction keeps the shared base roster (Harvester, MCV, Engineer, plus its own
Infantry/Tank/Gunship) and adds one unique seventh unit:

| Faction | Infantry | Tank | Gunship | Unique unit |
|---|---|---|---|---|
| Ashfall Compact | Sapper | Bulwark | Vulture | **Thumper** |
| Verdant Combine | Thorn | Stinger | Wasp | **Mantis** |
| Meridian Syndicate | Aegis | Lumen | Falcon | **Skyfall** |

Base-kind stats still use the Phase 19 faction modifiers; the names above are the
faction identities shown in the sidebar.

### Unique units

| Unit | Faction | Producer | Cost | HP | Damage | Range | Speed | Notes |
|---|---|---:|---:|---:|---:|---:|---:|---|
| Thumper | Ashfall | War Factory | 600 | 200 | 70 | 34 | 4 | long-range siege vehicle |
| Mantis | Verdant | Barracks | 250 | 45 | 7 | 12 | 6.5 | fast raider infantry |
| Skyfall | Meridian | War Factory | 1000 | 150 | 55 | 26 | 20 | air-superiority fighter |

The Skyfall is a true air unit (altitude flight, ignores nav blockers). The Thumper and
Mantis are ground units with distinct roles and procedural silhouettes.

## How they are selected

- The sidebar UNIT tab lists the **player faction's roster**, including the unique unit
  on hotkey `Y`.
- The AI builds its faction's unique unit once it has at least three ground units.
- Missions automatically use the assigned factions, so each mission fields the matching
  troops.
- Skirmish faction choice changes the roster immediately at match start.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 62/62, PlayMode 51/51.

### Covered by tests

- Each faction has a seven-entry roster containing its unique and not the others.
- Faction names for Infantry/Tank/Gunship are distinct.
- Unique units have their own producers, costs and role stats.
- The Skyfall spawns with the `AirUnit` component.

## Known limits (deliberate for Phase 20)

- Base Infantry/Tank/Gunship share silhouettes and only differ by name and faction
  modifiers; unique silhouettes exist only for the seventh unit.
- No faction-specific abilities yet (deploy artillery, cloak, shields, mines).
- No faction colour palettes or icons in the sidebar.
- Production hotkeys run Z X C V B G Y; only `Y` is newly assigned.
