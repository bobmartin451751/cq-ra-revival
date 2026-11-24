# Phase 79 - save slots and version migration

## What changed
- Three save slots, each at `persistentDataPath/cqra-save-slot{1..3}.json`.
- **F5** saves to the active slot and names the slot in the announcer.
- **F9** loads the active slot.
- **F7** cycles the active slot in-game.
- Main menu shows a **SLOT n** cycler, **LOAD** for the active slot and **REPLAY**.
- Save data now includes `savedUtc`; `SaveSlots.Describe` can show map, teams,
  difficulty and local time.
- `MatchSaveSerializer.Migrate` normalises legacy pre-versioned saves and rejects saves
  written by a newer build instead of partially loading them.

## Verification
- EditMode **111/111**
- PlayMode **89/89**
- Windows player built.

New coverage:
- legacy version-0 saves migrate and clamp map/team defaults
- future-version saves are rejected
- saved timestamp round trips
- slot paths are distinct and selection clamps

## Known limits
- Menu only shows a slot number; the full slot description is not drawn yet.
- The old single-file `cqra-save.json` is not imported automatically.
- Slot selection is per session, not persisted between launches.
