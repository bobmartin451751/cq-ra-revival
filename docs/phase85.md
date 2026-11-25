# Phase 85 - performance instrumentation

## What changed
- Added `PerformanceStats`, a rolling frame-time window (default 180 frames).
- The F3 overlay now shows:
  - average FPS and average frame time
  - worst frame time in the window
  - total ECS entity count
  - units, buildings, bridges and cached flow fields
  - current weather kind
  - managed memory in MB
  - the fixed simulation timestep
- Sampling is per frame; text refreshes twice a second to avoid HUD churn.
- The overlay remains development-only and toggled with **F3**.

## Verification
- EditMode **116/116**
- PlayMode **93/93**
- Windows player built.

New coverage:
- average/worst/FPS calculations and ring-buffer rollover
- negative samples clamp to zero

## Known limits
- Counts are point-in-time samples, not per-system timings.
- Managed memory is a rough indicator; native ECS allocations are not included.
- No automatic profiling capture or CSV export.
