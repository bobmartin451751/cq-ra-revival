# Phase 88 - choose your campaign branch

## What changed
- `MissionCatalog.NextMissionIndices` returns every valid branch, not just the first.
- `NextMissionIndex` remains as a compatibility wrapper around the first branch.
- After a win, the result screen now shows one button per branch:
  - "NEXT: <mission name>"
  - both branches are visible at once for missions 1 and 4
- RETRY and MAIN MENU stay on the first row; branch choices sit below them.
- The campaign result overlay is taller to fit branch rows.

## Verification
- EditMode **120/120**
- PlayMode **94/94**
- Windows player built.

New coverage:
- mission 1 exposes m2 and m3
- mission 4 exposes m6 and m7
- the finale and invalid indexes return no branches
- the legacy single-next helper still matches the first branch

## Known limits
- Branch choice appears only on win; failure still offers retry/menu.
- No preview of the next branch's map on the result screen.
