# Phase 61 - match snapshot serialisation

## What changed
Added the save-data layer for match snapshots:

- versioned `MatchSaveData`
- map/faction/team/difficulty metadata
- team credits
- unit snapshots: kind, team, position, health, veterancy kills
- building snapshots: kind, team, position, health
- JSON serialisation and file save/load helpers

This is the capture and serialisation half of save/load. Restoring a snapshot into
a fresh scene is still pending.

## Verification
- EditMode **93/93**
- PlayMode **79/79**
- Windows player built.

New coverage:
- capture units/buildings/credits
- JSON round trip
- file write/read round trip

## Known limits
- Snapshot restore is not implemented yet.
- Fog, research, AI commander state, ore, crates, garrison/cargo and hero progress
  are not captured yet.
- No save/load UI.
- No compatibility migration beyond the version field.
