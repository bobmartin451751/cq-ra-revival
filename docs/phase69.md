# Phase 69 - match replays

## What changed
- Matches record deterministic state keyframes every 15 sim ticks (0.5 s).
- Replays store map, factions, team count/mode, difficulty, observer flag, timestamp
  and the keyframe list.
- Replay files are gzip-compressed JSON at `persistentDataPath/cqra-replay.dat`.
- A replay is written automatically when a match ends.
- The main menu shows **WATCH REPLAY** when a replay exists.
- Playback rebuilds the match on the original map and applies each keyframe on its tick.
- AI decisions are suppressed during playback; player commands and save hotkeys are blocked.

## Verification
- EditMode **93/93**
- PlayMode **81/81**
- Windows player built.

New coverage:
- record keyframes, stop and produce a final frame
- gzip JSON round trip through disk
- playback applies frames in tick order and ends after the last frame

## Known limits
- Replays are snapshot-based rather than command-based, so between keyframes units
  stand still instead of re-simulating movement.
- Only the most recent replay is kept; no replay browser, seeking or speed control.
- Keyframes do not include projectiles, particles or announcer subtitles.
