# Phase 37 - 50 maps, 2-8 players and 15 missions

## Map capacity
The catalog now has **50 maps** in four size brackets:

| Tier | Players | Maps |
|---|---:|---:|
| Small | 2 | 8 |
| Medium | 3–4 | 12 |
| Large | 5–6 | 12 |
| Huge | 7–8 | 18 |

Every map carries:

- world size (220 / 320 / 420 / 520 m)
- minimum and maximum players
- a spawn list with one start per team
- its own terrain seed, water, ruins, ore and crates

The five original maps are the small-map baseline. The other 45 are additional
family variants across Ash Basin, Rusted Flats, Broken Ridge, Twin Shallows and
Flooded Delta.

## Multi-team match support
The engine now supports **2–8 teams**:

- per-team credits and power for eight teams
- one fog grid per active team
- eight team colours and building material sets
- one AI commander per AI team
- eight match-state alive counts
- FFA and team modes
- team mode splits the first half against the second half
  - 4 players → 2v2
  - 6 players → 3v3
  - 8 players → 4v4

Combat, auto-acquisition, turrets, mines and AI targeting use alliance checks, so
teams do not shoot each other.

## Skirmish setup
The menu now has:

- player count selector: 2–8
- mode: free-for-all or teams
- map filtering by player count
- paged map list: 6 maps per page
- map size/capacity description

Changing map, player count or mode reloads the scene with the settings preserved.

## Campaign
The campaign now has **15 missions**, each with:

- `mapId`
- team count
- team mode
- objective and difficulty

Campaign maps range from small 2-player maps to large 6-player battles and an
8-player final mission. Mission select is paged five at a time.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **80/80**
- PlayMode **66/66**
- Windows player built.

### New coverage
- eight-team economy and power state
- FFA/team alliance rules
- 50-map tier counts and spawn lists
- 15 missions with valid map references and supported team counts
- seven AI commanders for an eight-team match
- existing two-player tests still pass

## Known limits
- Hand-authored content is still limited to the five original maps; the additional
  45 are family variants with unique seeds, water and resource layouts.
- Map thumbnails are not generated yet; the browser uses text and metadata.
- Team assignment is auto-balanced, not custom per slot.
- Mission briefings are text-only.
