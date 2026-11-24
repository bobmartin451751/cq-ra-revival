# Phase 81 - bespoke map differentiation

## What changed
- Generated maps no longer repeat the same family layout. Per map:
  - spawn ring radius varies (0.37-0.394 of world size)
  - spawn ring rotation varies
  - basin radius varies (0.40-0.416 of world size)
  - max terrain height and noise scale vary slightly
  - water bodies are jittered (rotation, offset and 0.95-1.05 scale)
- Polygon lakes shift and scale around their own centre; circle lakes move and resize.
- Jitter is bounded so water still cannot reach the spawn ring (existing map safety
  tests continue to pass).
- Every one of the 50 maps now produces a distinct layout signature.

## Verification
- EditMode **112/112**
- PlayMode **90/90**
- Windows player built.

New coverage:
- all 50 maps have a unique combination of spawns, water, obstacles and ore positions

## Known limits
- This is strong procedural differentiation, not 50 individually hand-painted maps.
- Family silhouettes (ridge/delta/twin-lake) remain recognisable by design.
