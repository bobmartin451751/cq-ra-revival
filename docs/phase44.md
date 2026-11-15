# Phase 44 - 8-player fog performance

## What changed
Fog updates now cache the per-team fog entities, grid info and cell buffers once
per simulation tick instead of calling `FogQuery.TryGet` for every unit and
building.

Before:
- one fog query per unit
- one fog query per building
- repeated queries for allied-vision sharing and revision bumps

After:
- one cached lookup per team per tick
- units and buildings index the cached grid directly
- allied vision sharing and revision bumps reuse the same cache

This removes the largest repeated query cost in large 8-player battles while
keeping fog rules and shared team vision unchanged.

## Verification

- EditMode **90/90**
- PlayMode **76/76**
- Windows player built.

## Known limits
- No frame-time benchmark harness yet.
- AI unit/building scans still use repeated entity queries.
- Pathfinding and combat are not jobified.
- The fog optimization is structural; performance improvement has not been
  quantified on a low-end machine.
