# Phase 60 - deterministic command and snapshot foundation

## What changed
Added the shared foundation for save/load and replays:

- `SimCommand` record with tick, team, type, actor, target, kind and position.
- `CommandLog` for deterministic command capture.
- `SimClock` plus a fixed-step clock system.
- `MatchChecksum` over unit/building IDs, teams, kinds, health and positions.
- Command log and clock reset with runtime match state.

This phase does not yet record commands from gameplay or restore snapshots; it
establishes the stable primitives those systems will use.

## Verification
- EditMode **93/93**
- PlayMode **78/78**
- Windows player built.

New coverage:
- command record/clear behaviour
- clock advance/reset
- checksum stability and change detection

## Known limits
- Gameplay commands are not yet automatically appended to the log.
- No snapshot serialisation yet.
- Checksum covers positions/health/team/kind, not full AI/fog/research state.
