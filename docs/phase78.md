# Phase 78 - post-match history graphs

## What changed
- `MatchHistory` samples every 5 s: living units, credits and kills for teams 0 and 1.
- The history is presentation-only and never feeds the simulation.
- A final sample is taken when the match ends so the graph reaches the end state.
- History resets with the rest of the match runtime.
- The result screen now shows three graphs:
  - **ARMY** - living units
  - **CREDITS** - banked resources
  - **KILLS** - running kill totals
- Player series is warm orange, opponent series blue; each graph auto-scales.

## Verification
- EditMode **108/108**
- PlayMode **89/89**
- Windows player built.

New coverage:
- sampling captures per-team unit counts, credits and kills at the expected ticks

## Known limits
- Only teams 0 and 1 are graphed.
- Graphs appear on the skirmish result screen, not the campaign result overlay.
- Sampling is in-memory only and is not stored in replays.
