# Phase 22 - Thumper deploy-fire

## What exists

| Piece | Files |
|---|---|
| Deploy state component | `Assets/Scripts/Sim/Units/UnitComponents.cs` |
| Deploy toggle command | `Assets/Scripts/Sim/Base/BaseCommands.cs` (`TryToggleDeploy`) |
| Immobile deployed behaviour | `UnitMovementSystem.cs` |
| Spawn-time deploy data | `UnitFactory.cs` |
| Sidebar DEPLOY/UNDEPLOY action | `SelectionController.cs` |
| Tests | `Assets/Tests/PlayMode/ThumperDeployPlayModeTests.cs` |

## Behaviour

The Ashfall **Thumper** has two modes:

| Mode | Range | Cooldown | Movement |
|---|---:|---:|---|
| Mobile | 34 m | 3.0 s | normal |
| Deployed | 47.6 m (+40%) | 4.5 s | immobile |

- Selecting a Thumper shows **DEPLOY** in the command strip.
- Clicking it emplaces the unit: movement is cancelled, the weapon range increases by 40%
  and reload slows by 50%.
- While deployed the unit cannot move; movement orders are ignored.
- Selecting it again shows **UNDEPLOY**, restoring normal range, cooldown and mobility.
- Toggling is immediate (like RALLY/REPAIR); no right-click target is required.
- The AI does not yet deploy Thumpers intentionally.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 64/64, PlayMode 52/52.

### Covered by tests

- Deploy sets the deployed flag, increases range, slows cooldown and clears movement
  orders.
- Undeploy restores the normal range and cooldown.

## Known limits

- Only the Thumper has an ability so far.
- No deploy animation, recoil or ground-siege visual.
- Mantis cloak, Aegis shields, Sower mines and Rig repair aura remain.
