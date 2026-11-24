# Phase 70 - destructible bridges

## What changed
- Maps with water now get up to two neutral bridges crossing the narrow axis of each
  water body (`BridgeCatalog`, deterministic pure data).
- Bridge decks clear their water footprint in the nav grid, so infantry and vehicles
  can cross while the bridge stands.
- Every bridge is a neutral target with 900 health and a `Bridge` component.
- Bridges are never auto-acquired: they are only attacked by an explicit attack order.
- Destroying a bridge keeps the entity as rubble, blocks every deck nav cell and
  clears cached flow fields.
- Bridge state (health/destroyed) is included in saves, replays and snapshot checks.
- `WaterRenderer` builds code-first deck and railing meshes and switches them to
  rubble when the simulation reports destruction.

## Verification
- EditMode **96/96**
- PlayMode **82/82**
- Windows player built.

New coverage:
- bridge placement across circle water and empty-map handling
- nav cells on the deck are walkable while open water stays blocked
- destroying a bridge blocks its deck, is idempotent and leaves outside cells walkable

## Known limits
- Units still follow terrain height across a deck rather than the deck surface.
- AI does not deliberately target bridges yet.
- Bridges are rebuilt from map data on load; custom map-editor bridges are not a thing.
