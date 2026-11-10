# Phase 25 - AI deploy regression fix

## Problem

Skirmish AI never deployed its MCV when the match started. Because the opening army
spawns around the MCV, the building-placement validation rejected the deploy site as
`Occupied` (friendly units inside the yard footprint). `Think` returned after the failed
deploy every tick, so the AI never built, produced or attacked. The issue was
difficulty-independent and affected Recruit, Veteran, Hardened and Brutal.

## Fix

- **In-place deployment** (`TryDeployMcv`) now ignores friendly unit occupancy and relies
  on the existing evacuation step to push those units out of the new yard footprint.
- **Right-click deployment at a chosen site** (`TryDeployMcvAt`) keeps the strict unit
  check, so the player cannot drop a yard onto a crowd.
- Added an `ignoreUnits` path to the occupancy validator.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase25-ai-easy.png -Ai -AiLevel 0 -WaitFrames 1500
```

Suites: EditMode 64/64, PlayMode 56/56.

### Covered by tests

- In-place deployment succeeds even when friendly units surround the MCV, and those
  units are evacuated rather than deleted.
- A new startup smoke test runs **Recruit, Veteran, Hardened and Brutal** AI: each must
  deploy its Construction Yard and add at least one more structure within six seconds.

The Recruit capture (`-AiLevel 0`) shows both bases built and producing units.
