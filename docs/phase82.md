# Phase 82 - content audit and tooling

## What changed
- Added `ContentAudit`, a pure validator shared by tests and editor tooling.
- Map checks:
  - unique ids and seeds
  - valid world/basin sizes
  - spawn count matches player count, in-basin and out of water
  - minimum obstacles/ore/crates
  - resources inside the playable area and off water
- Campaign checks:
  - unique mission ids, known maps, codex present, valid team counts
  - branch/requirement links resolve and never self-link
- Faction checks:
  - exactly three factions, unique names and a unique unit each
- Editor menu: **CQRA > Validate Content** logs the report and shows a dialog.
- `ContentAudit.Format` produces a readable multi-line summary.

## Verification
- EditMode **114/114**
- PlayMode **90/90**
- Windows player built.

New coverage:
- the full audit reports zero issues
- map, mission and faction validators run independently

## Known limits
- The audit is data-only; it does not inspect scene objects or materials.
- No automated fix-up for issues it finds.
