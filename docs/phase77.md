# Phase 77 - AI superweapons

## What changed
- The AI now builds a Superweapon once it has more than 3000 credits and no existing one.
- The AI fires a charged superweapon at the closest visible enemy structure within
  260 m, falling back to the closest visible enemy unit.
- Targeting uses the **same fog grid** as the player, so the AI cannot fire through fog.
- Firing goes through `SuperweaponCommands`, so charge timing, faction strike kind,
  damage and cooldowns are identical to the player's.
- No target means the charge is retained instead of wasted.

## Verification
- EditMode **108/108**
- PlayMode **88/88**
- Windows player built.

New coverage:
- a charged AI superweapon fires at a visible structure and consumes the charge
- a second call without a recharge is rejected
- no enemies in sight means no fire and no charge loss

## Known limits
- The AI still does not use hero abilities.
- Strike targets are single points; it does not try to hit clustered armies.
