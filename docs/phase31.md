# Phase 31 - naval yard shore placement

## What changed
- The **Naval Yard** now has to be built near water.
  - Placement checks the distance from the lake centre; anything farther than
    `lake radius + 28 m` is rejected with **"Naval Yard must be near the lake"**.
  - The normal Construction Yard build-radius rule does not apply to the Naval
    Yard, so it can be placed on the shoreline instead of crowding the base.
- AI naval expansion now searches the shoreline:
  - `BaseCommands.TryAutoPlaceNavalYard` walks 24 points around the lake at
    `lake radius + 8 m` and takes the first legal site.
  - `AiSystem` uses that instead of the generic base build ring.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

- EditMode **65/65**
- PlayMode **62/62** (two new naval placement tests)
- Windows player built.

### New tests
- A Naval Yard far from the lake is rejected with `BuildFailure.NotNearWater`
  and no credits are spent.
- A Naval Yard on the lake shore is accepted and its cost is deducted.
- The AI auto-placement helper finds and places a Naval Yard near the lake.

## Known limits
- The lake is still the only water body and the only naval construction site.
- Ships still teleport into the lake from the yard.
- There is no dock outlet or sailing-out animation.
