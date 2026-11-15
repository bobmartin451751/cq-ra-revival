# Phase 42 - specialist infantry

## Shared Barracks soldiers
Four new specialist roles are available to every faction:

| Unit | Hotkey | Cost | Role |
|---|---|---:|---|
| Anti-Tank | H | 220 | Ground vehicle/building killer, 1.5× vs vehicles |
| Anti-Air | J | 180 | Air-only missile infantry |
| Grenadier | K | 230 | Long-range anti-structure infantry |
| Medic | L | 150 | Unarmed healing aura |

### Field notes
- Anti-Tank: 60 HP, 45 damage, 18 range, slow.
- Anti-Air: 55 HP, 12 damage, 30 range, air-only.
- Grenadier: 60 HP, 30 damage, 26 range, slow.
- Medic: 45 HP, heals nearby friendly infantry at 5 HP/s within 14 m.

All specialists are infantry for transport and bunker purposes.

## Faction uniques
- **Ashfall:** Breacher — 280 credits, short-range 18-damage burst infantry.
- **Verdant:** Mantis — existing cloaked swarm infantry.
- **Meridian:** Marksman — 300 credits, 50 damage, 36 range sniper.

## Production
- All six are produced by the Barracks.
- Barracks rows now show only Barracks units.
- Hotkeys: Z infantry, G engineer, H anti-tank, J anti-air, K grenadier,
  L medic, Y faction unique.
- AI buys a Medic, Anti-Tank and Anti-Air as its army develops.

## Verification
- EditMode **90/90**
- PlayMode **76/76**
- Windows player built.

New coverage:
- every faction has all shared specialists
- faction unique barracks infantry
- specialist air/ground targeting
- Medic healing aura

## Known limits
- No area-of-effect grenade damage yet; Grenadier is direct-fire.
- Medic only heals infantry, not vehicles/buildings.
- Anti-Tank bonus is a flat multiplier against vehicles/buildings.
- No dedicated specialist animations or portraits.
