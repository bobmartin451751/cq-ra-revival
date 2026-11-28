# Phase 87 - full-screen mission briefings

## What changed
- Clicking **START MISSION** now opens a full-screen briefing instead of starting instantly.
- The briefing shows:
  - mission name and large rendered map preview
  - objective and briefing text
  - codex/lore block
  - map, player count and difficulty
  - the next campaign branch names
  - the faction hero and its career kills
- **DEPLOY** starts the mission; **BACK** returns to the campaign list.
- `CampaignBriefing` centralises the text so tests and UI share one source.
- `MissionCatalog.NextMissionNames` exposes branch names for briefings and menus.

## Verification
- EditMode **118/118**
- PlayMode **94/94**
- Windows player built.

New coverage:
- briefing text includes objective, briefing, codex, map and next branches
- the finale reports "Campaign finale"
- null missions produce empty text

## Known limits
- No voice-over or typewriter effect.
- Briefing does not let you pick between branches before starting; the win screen
  still follows the first branch.
