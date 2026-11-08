# Phase 24 - Mantis cloak and proximity detection

## What exists

| Piece | Files |
|---|---|
| Cloak component | `Assets/Scripts/Sim/Units/UnitComponents.cs` |
| Cloak state system | `Assets/Scripts/Sim/Units/CloakSystem.cs` |
| Proximity detection | `CloakQuery` in `CloakSystem.cs` |
| Spawn assignment | `UnitFactory.cs` (Verdant Mantis) |
| Targeting/selection/render hooks | `CombatSystem.cs`, `AiSystem.cs`, `FogVisibilitySystem.cs`, `SelectionController.cs` |
| Tests | `Assets/Tests/PlayMode/FactionPlayModeTests.cs` |

## Behaviour

The Verdant **Mantis** cloaks itself when it has been stationary for **3 seconds**.

- Moving breaks the cloak immediately.
- While cloaked it is:
  - hidden from enemy auto-acquisition and AI target search,
  - hidden from the player's rendering and click-picking,
  - not hidden from explicit attack orders once already fired (orders persist).
- **Proximity detection:** any enemy unit or building within **10 m** reveals the Mantis.
  This applies to both teams equally.
- The cloak does not affect fog; the Mantis still reveals map cells for its own team.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 64/64, PlayMode 54/54.

### Covered by tests

- A stationary Mantis cloaks after the delay.
- It is hidden from distant enemies.
- Moving within 10 m reveals it.
- Moving breaks the cloak.

## Known limits

- No dedicated detector units or detector vision cone yet; detection is proximity-only.
- No cloak shimmer/visual effect.
- Only the Verdant Mantis has cloak.
- Explicit attack orders already issued continue to track a unit after it cloaks.
