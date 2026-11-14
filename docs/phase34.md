# Phase 34 - campaign expansion

## What changed
The campaign now has **five missions**, completing the mission-select list without
needing a scrolling UI.

| # | Mission | Objective | Difficulty | Player | AI |
|---:|---|---|---|---|---|
| 1 | First Contact | destroy all | Recruit | Ashfall | Verdant |
| 2 | Hold the Line | survive 5 min | Veteran | Meridian | Verdant |
| 3 | Break Their Base | destroy the yard | Hardened | Ashfall | Meridian |
| 4 | Iron Tide | survive 6 min | Hardened | Verdant | Meridian |
| 5 | The Last Compact | destroy the yard | Brutal | Meridian | Ashfall |

- The built-in fallback mission list now matches `Assets/Resources/Data/missions.json`.
- The final mission is a Brutal-difficulty base assault against Ashfall.
- Existing progression still works: a mission unlocks when the previous one is
  completed, and the victory screen offers **NEXT MISSION**.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **71/71**
- PlayMode **63/63**
- Windows player built.

### New tests
- Missions load from JSON with exactly five unique, fully populated entries.
- The finale is a Brutal `destroyYard` mission: Meridian vs Ashfall.
- All three factions appear across the campaign.

## Known limits
- Missions still reuse the same basin map and AI profile system.
- No persistent hero units, branching objectives, interactive briefings or codex yet.
- The campaign menu is capped at five missions until scrolling is added.
