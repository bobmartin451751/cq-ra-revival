# Phase 43 - AI transport missions

## AI transport use
The AI now builds and uses one transport helicopter:

1. Builds an Airfield.
2. Produces its faction's heavy transport.
3. Loads up to five nearby infantry.
4. Flies to the nearest visible enemy target.
5. Unloads the squad when within 18 m.
6. Repeats when possible.

If the target dies or disappears, the transport unloads immediately and clears
its mission.

## Implementation
- New `AiTransportMission` component tracks the target and flight phase.
- AI transport production is separate from fighter/bomber/attack-helicopter
  production.
- Loading and unloading reuse the same `TransportCommands` as the player.
- AI still pays normal production costs and obeys Airfield requirements.

## Verification

- EditMode **90/90**
- PlayMode **76/76**
- Windows player built.

## Known limits
- One transport mission per AI commander.
- The AI does not use transport helicopters for retreats or flanking.
- Transport missions target the nearest visible enemy and do not coordinate
  multiple drops.
