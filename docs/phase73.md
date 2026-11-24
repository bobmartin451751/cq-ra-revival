# Phase 73 - visual weather

## What changed
- Added `WeatherCatalog`: stable per-map weather using an FNV hash of the map ID
  (not `string.GetHashCode`, which varies per process).
- Four kinds: Clear, Rain, Dust Storm and Snow.
- `WeatherDirector` builds code-first particle effects and scene tinting:
  - rain: stretched streaks and cool fog
  - snow: slow drifting flakes and pale fog
  - dust: wind-driven brown particles and amber fog
- Weather is spawned automatically by `GameBootstrap`; no scene edits needed.
- The emitter follows the camera so weather stays visible at any zoom.
- **Weather is visual-only.** It does not touch ECS, vision, movement, accuracy or damage.
- Fog densities were tuned down after a screenshot smoke check so the battlefield stays
  readable instead of washing out.

## Verification
- EditMode **101/101**
- PlayMode **84/84**
- Windows player built.
- Screenshot smoke check: `.temp/screens/phase73b.png`.

New coverage:
- weather choice is deterministic and always valid
- null/empty map IDs fall back to Clear
- the catalogue contains clear, rain, dust and snow maps

## Known limits
- No weather transitions; each map has one fixed condition.
- No rain audio, lightning, puddles or wind sound.
- Fog is global and does not interact with the simulation fog of war.
