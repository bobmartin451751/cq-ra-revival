# Phase 30 - naval AI and faction scale polish

## Naval AI
- AI now builds one **Naval Yard** once it has a War Factory and credits above 1200.
- It then maintains **1 Patrol Boat** (2 on Hardened/Brutal) through the normal
  production queue and naval spawn rules.
- AI army orders now include **all armed units**, not just infantry and tanks, so
  uniques and ships are no longer left idle.

## Faction polish
- Unit visual scale now follows the faction:
  - Ashfall: 1.00x
  - Verdant: 0.92x (smaller, faster-looking)
  - Meridian: 1.08x (larger, heavier-looking)
- Building materials already re-tint to faction colours at match start (Phase 29).
- UI production rows hide the hotkey brackets for naval entries that have no key.

## Verification

```powershell
.\tools\run-tests.ps1
.\tools\build.ps1
```

Suites: EditMode 65/65, PlayMode 60/60.

## Known limits
- Ships still spawn teleported into the lake from any Naval Yard and cannot leave it.
- AI naval units can only engage targets near the lake.
- Scale is presentation-only; collision radii stay at gameplay values.
- No faction-unique base silhouettes or faction buildings yet.
