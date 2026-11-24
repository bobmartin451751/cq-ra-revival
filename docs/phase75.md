# Phase 75 - branching campaign graph and codex

## What changed
- Missions now carry a hand-authored graph: `next` ids and `requires` ids.
- The 15-mission campaign branches and rejoins:
  - mission 1 splits into missions 2 and 3
  - missions 4 and 5 feed different follow-ups
  - mission 7 has two valid routes in
  - missions 13 and 14 both lead to the finale
- Unlock rules use completed prerequisites; missions without graph data fall back to
  the old linear unlock.
- Campaign menu shows the mission **CODEX** entry and its available branches.
- After a win, **NEXT MISSION** follows the first branch defined by the graph instead
  of always using index + 1.
- `missions.json` can override any graph or codex field; missing fields are filled from
  a built-in table.

## Verification
- EditMode **107/107**
- PlayMode **84/84**
- Windows player built.

New coverage:
- all 15 missions have ids, codex text and real map ids
- branch and requirement ids resolve
- mission 1 splits, mission 7 has two routes, the finale is a dead end
- next-mission routing follows the graph

## Known limits
- The campaign menu shows one next mission at a time; there is no drawn graph view.
- Completion is still stored in PlayerPrefs, not a save slot.
- No mission-select replay of a completed branch after the finale.
