# Phase 41 - research and upgrades

## Research tab
The sidebar now has three tabs:

- **BUILD**
- **UNITS**
- **TECHS**

The TECHS tab offers three team-wide upgrades:

| Upgrade | Cost | Time | Effect |
|---|---:|---:|---|
| Weapon Tuning | 700 | 26 s | +10% damage |
| Armour Plating | 650 | 24 s | +10% health |
| Logistics | 600 | 20 s | +10% movement speed |

- One research job per team at a time.
- Completed upgrades are permanent for the match.
- Effects apply immediately to existing units and buildings.
- Newly spawned units and buildings inherit completed upgrades.
- The AI researches upgrades when it has spare credits.

## UI
Each row shows the upgrade name, effect, cost/time, or current state:

- `RESEARCH`
- `RESEARCHING n s`
- `DONE`

## Verification

- EditMode **87/87**
- PlayMode **75/75**
- Windows player built.

The research test covers:

- starting research and paying credits
- completing a research job
- boosting existing units
- spawning new units with the upgrade already applied

## Known limits
- No upgrade-specific art or announcer lines.
- No per-unit or per-building upgrade choices.
- Research is team-wide only.
- AI research order is fixed by the catalog order.
