# Phase 54 - base-under-attack warning

## What changed
When an enemy attack damages a player building, the announcer now displays:

```
Base under attack
```

A short radio cue plays and the warning is rate-limited to once every 8 seconds
so sustained combat does not spam the HUD.

## Verification

- EditMode **91/91**
- PlayMode **77/77**
- Windows player built.

## Known limits
- Building damage is the only trigger; unit losses do not trigger it.
- No directional indicator or minimap ping.
- No voice-over, subtitle only.
