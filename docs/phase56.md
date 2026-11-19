# Phase 56 - performance overlay

## What changed
Added an F3 development overlay showing:

- FPS
- live unit count
- live building count
- a short hide hint

The overlay updates twice per second and is generated into the scene by
SceneBuilder. It is hidden by default.

This is a benchmarking aid for large 8-player matches; it does not affect the
simulation.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- No per-system timings or graphs.
- FPS is frame rate, not fixed-step simulation duration.
- No automated benchmark scene or recorded baseline.
