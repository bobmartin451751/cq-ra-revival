# Phase 39 - Airfields, 12 aircraft, AA and transport helicopters

## Airfield
New **Airfield** building:

- cost 1100, power 50, HP 900
- owns the aircraft production queue
- fighters and bombers return there to rearm
- AI builds and uses it

Gunship and Skyfall production moved from the War Factory to the Airfield.
The War Factory no longer builds aircraft.

## Aircraft roster
Each faction has four aircraft.

### Ashfall
- Gunship — attack helicopter, carries 5 soldiers
- Warden — fighter
- Cinder — bomber
- Mule — heavy transport

### Verdant
- Kite — fighter
- Blight — bomber
- Scythe — attack helicopter, carries 5 soldiers
- Hauler — heavy transport

### Meridian
- Skyfall — fighter
- Comet — bomber
- Talon — attack helicopter, carries 5 soldiers
- Atlas — heavy transport

Fighters are air-only. Bombers are ground-only. Attack helicopters can engage
both. Transports are unarmed.

## Rearming
Fighters and bombers carry limited ammo.
When ammo reaches zero:

1. they clear their target
2. fly back to their home Airfield
3. land on the pad
4. rearm for 4 seconds
5. relaunch when ordered

Destroyed Airfields make aircraft look for another friendly Airfield.

## Transport helicopters
Cargo rules are implemented and tested:

- attack helicopter: **5 soldiers**
- heavy transport: **20 soldiers**, or **1 tank + 5 soldiers**
- load: select soldiers/tank, right-click a friendly transport
- unload: select the transport and press **E**
- cargo dies with the transport
- transport UI shows cargo count and capacity

AI aircraft production is implemented; AI does not use transport helicopters yet.

## AA Gun
New **AA Gun** building:

- cost 450, power 10, HP 380
- air-only targeting
- 16 damage, 34 range, 0.55 s cooldown
- deliberately weaker than a Gun Tower against ground

Gun Towers now target ground only. Weapons use explicit ground/air target masks.

## 8-player polish
Included:

- shared allied fog/vision
- custom team assignment toggles in the skirmish menu
- team alliance API supporting arbitrary A/B groups
- allied AI commanders join an attack when another allied AI is already attacking
- AI cycles its aircraft production between fighter, bomber and attack helicopter roles

Still outstanding:

- odd-player auto-balance
- measured performance tuning on 8-player huge maps

## Verification

- EditMode **87/87**
- PlayMode **73/73**
- Windows player built.

## Known limits
- Aircraft rearming uses vertical pad landing, not runways.
- AI transport use is not implemented.
- Fighter/bomber rearming has a PlayMode regression test.
- The new aircraft are simple procedural silhouettes rather than highly detailed models.
