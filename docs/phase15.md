# Phase 15 - campaign framework

## What exists

| Piece | Files |
|---|---|
| Mission DTOs, loader, progression | `Assets/Scripts/Sim/Campaign/MissionCatalog.cs` |
| Campaign menu, briefing, objectives, result flow | `Assets/Scripts/Core/SkirmishController.cs` |
| Mission data | `Assets/Resources/Data/missions.json` |
| Tests | `Assets/Tests/EditMode/Phase15Tests.cs` |

## Flow

1. The start menu now has **SKIRMISH** and **CAMPAIGN** tabs.
2. Campaign lists missions in order. The first is always unlocked; each win unlocks the
   next. Completed missions are marked with `*`; locked missions are greyed out.
3. Selecting a mission shows its briefing. **START MISSION** pauses the menu and starts
   the match paused-free with the mission's AI difficulty.
4. During a mission the top-left panel shows the mission name and live objective
   (including a survive countdown).
5. Victory/failure shows **MISSION COMPLETE / MISSION FAILED** with **RETRY**,
   **NEXT MISSION** (if won and available) and **MAIN MENU**. Progress is stored in
   `PlayerPrefs`; the next mission auto-starts after the scene reload.

## Missions

| # | Name | Difficulty | Objective |
|---|---|---|---|
| 1 | First Contact | Recruit | Destroy all enemy units and structures |
| 2 | Hold the Line | Veteran | Survive 5 minutes |
| 3 | Break Their Base | Hardened | Destroy the enemy Construction Yard |

Mission data lives in `Assets/Resources/Data/missions.json`:

```json
{
  "missions": [
    {
      "id": "m1",
      "name": "First Contact",
      "briefing": "...",
      "difficulty": "Recruit",
      "objective": "destroyAll",
      "objectiveText": "Destroy every enemy unit and structure.",
      "surviveSeconds": 0
    }
  ]
}
```

Supported objectives: `destroyAll`, `survive`, `destroyYard`. Difficulty maps to the same
four AI tiers and never changes costs, health or vision.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 53/53, PlayMode 45/45.

### Covered by tests

- The mission catalog has at least three playable missions with name, briefing and
  objective.
- Every mission objective is one of the supported types.
- The survive mission has a positive timer.
- The first mission is always unlocked.

The campaign menu is IMGUI and therefore not captured by the camera screenshot rig; use
the editor or the Windows build.

## Known limits (deliberate for Phase 15)

- Three missions on the standard map with the standard starting forces.
- No custom mission maps, scripted dialogue, voice-over or cutscenes.
- No mid-mission save/load (restart or next mission only).
- Briefings are plain text; no portraits or briefing art yet.
- Campaign progress is per-machine `PlayerPrefs`, not a profile system.
