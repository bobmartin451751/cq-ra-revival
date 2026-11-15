# Phase 45 - match statistics

## What changed
Matches now track per-team statistics:

- units built
- kills
- losses
- buildings destroyed
- buildings lost

Combat kills and production completions feed the statistics automatically.
The end-of-match screens show the player's totals:

```
Built N | Kills N | Losses N | Buildings destroyed N
```

This applies to both skirmish and campaign result screens.

## Verification

- EditMode **90/90**
- PlayMode **77/77**
- Windows player built.

## Known limits
- No graph or per-team breakdown yet.
- Resources harvested and credits spent are not tracked.
- Statistics reset on scene reload.
- No persistent career totals.
