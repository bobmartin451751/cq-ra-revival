# Phase 5 - base and economy core

## What exists

| System | Files |
|---|---|
| Buildings, economy, harvester and ore data + rules | `Assets/Scripts/Sim/Base/BaseComponents.cs` |
| Building/ore entity factory | `Assets/Scripts/Sim/Base/BuildingFactory.cs` |
| MCV deploy, placement validation, auto-build helper | `Assets/Scripts/Sim/Base/BaseCommands.cs` |
| Credits/power singleton and power totals | `Assets/Scripts/Sim/Base/EconomySystem.cs` |
| Autonomous harvester loop | `Assets/Scripts/Sim/Base/HarvesterSystem.cs` |
| Runtime ore authoring | `Assets/Scripts/Sim/Base/EconomyAuthoring.cs` |
| Procedural building/ore materials | `Assets/Scripts/View/BaseRenderer.cs` |
| Build panel, deploy key, HUD | `Assets/Scripts/View/SelectionController.cs` |

Everything runs in the 30 Hz fixed-step simulation. The only procedural meshes are
generated at scene start; there are no prefabs or imported assets.

## Economy

`EconomyState` is a singleton with per-team credits and power totals.

- Both teams start with **3000 credits**.
- Power is recomputed every tick from `BuildingInfo`: the yard produces 10, a power
  plant produces 100, a refinery consumes 40.
- Credits are only spent by build placement and only earned by harvester deliveries.
- The HUD shows the player team's credits and `used/made` power.

## Buildings and placement

| Building | Cost | Power | Footprint | Nav block | HP |
|---|---:|---:|---:|---:|---:|
| Construction Yard | deployed | +10 | 9 m | 9 m | 1200 |
| Power Plant | 300 | +100 | 6 m | 6 m | 600 |
| Refinery | 600 | -40 | 9 m | 9 m | 900 |

Flow:

1. Select an MCV and press **D**. The MCV is consumed and a Construction Yard appears.
   Deploying a second yard for the same team is rejected.
2. Press **1** for a Power Plant or **2** for a Refinery, then left-click the ground.
3. Placement is rejected when the site is outside 45 m of the team's yard, unaffordable,
   blocked by terrain/scenery, or overlapping another building, unit or ore patch.
4. A placed building blocks the nav grid under its footprint and invalidates cached flow
   fields, so units immediately path around it. Any unit accidentally sealed inside the
   footprint is moved to the nearest walkable cell.

Buildings are static: they have no `UnitTag`, are not moved by steering, and currently
cannot be targeted or destroyed. That is deliberate until the building-combat pass.

## Harvesters

Harvesters are unarmed units with a `HarvesterJob` component:

`Idle -> ToOre -> Harvesting -> ToRefinery -> Idle`

- Idle harvesters need a refinery for their team; without one they stay put.
- Harvesting fills cargo at 60 ore/second up to 700 cargo.
- A full load (or an exhausted patch) sends the harvester to the nearest refinery.
  Within 20 m of the refinery the cargo is converted to the team's credits.
- Ore patches are finite entities with an `OreNode.Amount`; they are depleted, not
  removed, so the loop deterministically picks the next nearest patch.
- The loop only writes move orders when the harvester has no order, so a player move
  order always wins until the harvester arrives. `HarvesterJob.AutoOrder` distinguishes
  the loop's own orders from player orders, so passing near an ore patch or refinery
  cannot cancel a player order.

`EconomyAuthoring` places 10 deterministic ore patches and skips any that land on
non-walkable terrain.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\screenshot.ps1 -Out .temp/screens/phase5-base.png -Base -WaitFrames 1400
```

`-base` deploys both MCVs and places a power plant plus a refinery for each team through
the normal build rules, then lets the harvesters run.

Suites: EditMode 28/28, PlayMode 22/22.

### Covered by tests

- Build catalog costs/power/footprints and the two player-buildable kinds.
- Build-radius and footprint-overlap math.
- Credit spending, insufficient funds and per-team independence.
- MCV stats and the fourth renderable unit kind.
- Auto-build: MCV deployed, power plant and refinery placed, credits deducted, power
  totals recomputed.
- Placement rejected outside the build radius and when credits are insufficient.
- Harvester deposits cargo at a refinery.
- Harvester finds ore, drives to it and depletes the patch.
- A player move order is not cancelled by passing next to an ore patch.

## Known limits (deliberate for Phase 5)

- No production queues or unit construction yet; the build panel places structures.
- No building combat, repair, sell or power-brownout effects.
- Ore is a single node entity per patch with no visual regrowth.
- One Construction Yard per team; no multiple-yard build-radius union.
- The AutoBuild helper is a capture/debug tool, not an AI base planner.
