# Phase 6 - production and unit construction

## What exists

| System | Files |
|---|---|
| Production tags, queue, rally point and catalog | `Assets/Scripts/Sim/Base/ProductionComponents.cs` |
| Enqueue rules + production system | `Assets/Scripts/Sim/Base/ProductionSystem.cs` |
| Barracks / War Factory specs and placeability | `Assets/Scripts/Sim/Base/BaseComponents.cs` |
| Building spawns incl. production buildings | `Assets/Scripts/Sim/Base/BuildingFactory.cs` |
| Building selection highlight | `Assets/Scripts/Sim/Base/BuildingHighlightSystem.cs` |
| Building picking, production HUD, rally orders | `Assets/Scripts/View/SelectionController.cs` |
| Scene unit visuals for produced units | `Assets/Scripts/Sim/Units/UnitFactory.cs`, `UnitSpawner.cs` |

Everything runs on the same 30 Hz fixed-step clock. Production costs are paid when the
order is enqueued, and completion is a deterministic entity spawn.

## New buildings

| Building | Cost | Power | Footprint | HP |
|---|---:|---:|---:|---:|
| Barracks | 400 | -20 | 7 m | 500 |
| War Factory | 900 | -60 | 11 m | 800 |

Both are in the build panel (keys **3** and **4**). Placing them follows the same
credit, build-radius, terrain, overlap and nav-blocking rules as Phase 5.

## Production

| Unit | Cost | Time | Produced by |
|---|---:|---:|---|
| Infantry | 100 | 4 s | Barracks |
| Tank | 350 | 8 s | War Factory |
| Harvester | 500 | 10 s | War Factory |
| MCV | 1000 | 12 s | War Factory |

Rules:

- **Queue.** Each production building has a 5-item FIFO queue. Only the front item
  advances; the HUD shows its remaining seconds and the rest as full build times.
- **Multiple producers.** The selected building is preferred; if its queue is full the
  next barracks/factory of the team with room takes the order.
- **Payment.** Credits are deducted at enqueue time. Enqueue fails on no producer,
  insufficient credits, or a full queue, and leaves the balance untouched.
- **Completion.** The order is consumed, then the unit spawns just outside the
  building's nav footprint, is clamped to a walkable cell, and receives a move order to
  the building's rally point.
- **Rally.** A production building's rally defaults to 22 m toward the map centre.
  Select only production buildings and right-click terrain to move their rally.
- **Selection.** Buildings can now be clicked (and box-selected). They use a dedicated
  highlight material, and the HUD reports their health.
- **Controls.** `Z` Infantry, `X` Tank, `C` Harvester, `V` MCV. The selected production
  building is preferred; otherwise the first matching building for the team is used.
- **Production never stops for power.** Power is displayed but brownouts are a later
  phase.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase6-army.png -Army -WaitFrames 2400
```

`-army` deploys both bases, places a barracks and war factory per team through the real
build rules, and queues one infantryman and one tank per team.

Suites: EditMode 32/32, PlayMode 25/25.

### Covered by tests

- Production catalog maps every unit kind to the right producer with sane costs/times.
- Barracks and War Factory specs: cost, power draw and footprint.
- Production hotkeys are unique.
- Barracks spawns an infantryman, consumes the queue entry and pays 100 credits.
- The produced unit receives a move order whose destination is the rally point.
- Enqueue fails without a producer, without credits, and with a full 5-item queue.
- A full selected producer falls back to another producer of the same kind with room.
- Failed enqueues never modify the credit balance.

## Known limits (deliberate for Phase 6)

- No queue cancellation or refund.
- No power brownout, production speed or multiple-queue parallelism.
- No unit population cap.
- Buildings are still not targetable; production continues through combat.
- The capture auto-build helper is not an AI base planner.
