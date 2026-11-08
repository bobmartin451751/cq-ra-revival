# Phase 14 - content pipeline (JSON -> BlobAsset)

## What exists

| Piece | Files |
|---|---|
| Definitions, defaults, JSON parsing, BlobAsset | `Assets/Scripts/Sim/Content/ContentDatabase.cs` |
| Scene loader | `Assets/Scripts/Sim/Content/ContentLoader.cs` |
| Data file | `Assets/Resources/Data/content.json` |
| Scene wiring | `SceneBuilder.cs` (Content object) |
| Tests | `Assets/Tests/EditMode/Phase14Tests.cs` |

## How it works

1. `ContentLoader` (execution order -400) loads `Resources/Data/content.json` before
   `UnitSpawner` and `EconomyAuthoring` run.
2. `ContentDatabase.LoadFromJson` parses the file with `JsonUtility`, overlays it on the
   shipped defaults, and compiles the result into a `BlobAssetReference<ContentBlob>`.
3. Gameplay accessors read the compiled values:
   - `CombatStats.For` - health, armour, damage, range, cooldown, acquisition
   - `ProductionCatalog.Get` - cost, build time, producer, label
   - `BuildCatalog.Get` - cost, power, footprint, nav radius
   - `VisionCatalog` - unit/building vision
   - `UnitFactory.RadiusFor/SpeedFor`
   - `BuildingFactory` health
4. If the asset is missing or parse fails, the hardcoded defaults remain active, so tests
   and bare scenes still work.

## Editing content

Change `Assets/Resources/Data/content.json` and relaunch; no gameplay code changes.

```json
{
  "units": [
    {
      "kind": "Tank",
      "cost": 350,
      "buildSeconds": 8.0,
      "producer": "WarFactory",
      "maxHealth": 260,
      "armor": 0.35,
      "damage": 45,
      "range": 24,
      "cooldown": 1.6,
      "acquireRange": 28,
      "speed": 9,
      "radius": 3.4,
      "vision": 40,
      "label": "Tank"
    }
  ],
  "buildings": [
    {
      "kind": "Barracks",
      "cost": 400,
      "powerProduced": 0,
      "powerUsed": 20,
      "radius": 7,
      "navRadius": 7,
      "health": 500,
      "vision": 28
    }
  ]
}
```

Every entry is optional; missing fields fall back to defaults, unknown kinds are ignored
with a warning.

## Verification

```powershell
.\tools\run-tests.ps1
```

Suites: EditMode 50/50, PlayMode 45/45.

### Covered by tests

- Defaults match the established values for cost, health, wall price, MCV radius and tank
  vision.
- A JSON override changes cost, build time, label, health, damage, vision and building
  power/cost through every accessor.
- Resetting restores the defaults.
- Unknown unit and building kinds are ignored without breaking the table.

## Known limits (deliberate for Phase 14)

- One global table; no per-faction variants yet.
- No hot reload; content is compiled at scene start.
- Strings are minimal (labels only); full localisation comes later.
- JSON is loaded from `Resources` rather than addressables/mods.
