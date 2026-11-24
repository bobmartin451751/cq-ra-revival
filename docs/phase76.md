# Phase 76 - persistent hero abilities

## What changed
- Campaign heroes now carry one persistent faction ability.
- The ability unlocks at **5 career kills** (the same kills carried between missions).
- Each faction gets a distinct effect within 20 m:
  - **Ashfall - Overdrive**: allies gain a 0.65x weapon cooldown for 8 s and current
    cooldowns reset.
  - **Verdant - Regrowth**: allies heal 80 instantly and regenerate 15 HP/s for 6 s.
  - **Meridian - Aegis**: allies gain/replace an 80-point shield that regenerates.
- Ability cooldown is 45 s and ticks in the fixed-step simulation.
- Selection UI shows the ability state (kills needed, cooldown or READY) and an
  ABILITY / RECHARGING button.
- Heroes spawn with the ability and their persisted kill count.

## Verification
- EditMode **108/108**
- PlayMode **86/86**
- Windows player built.

New coverage:
- faction-to-ability mapping and kill threshold
- Overdrive buffs an ally and enforces cooldown
- Regrowth heals a wounded ally over time
- Aegis grants a shield

## Known limits
- Cooldown does not persist between missions; only kills do.
- The AI has no hero and does not use abilities.
- No ability VFX or voice line yet.
