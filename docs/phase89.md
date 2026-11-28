# Phase 89 - save slot details in the menu

## What changed
- The main menu now shows a live description of the active save slot under the
  SLOT/LOAD/REPLAY row.
- Descriptions include slot number, map name, player count, difficulty and local save
  time, or "EMPTY" when unused.
- Submenu content shifts down 14 px to make room, keeping every existing button inside
  the panel.
- Cycling slots immediately updates the description.

## Verification
- EditMode **121/121**
- PlayMode **94/94**
- Windows player built.

New coverage:
- every slot description is prefixed with its slot number

## Known limits
- Descriptions are read from disk once per frame; there is no cached metadata index.
- No thumbnail or screenshot of the saved match.
