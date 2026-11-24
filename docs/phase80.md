# Phase 80 - announcer and acknowledgement audio

## What changed
- Added six procedural radio cues:
  - **AckMove** - rising two-note confirmation on move orders
  - **AckAttack** - falling two-note confirmation on attack orders
  - **AckBuild** - three-note completion when production finishes
  - **Announcer** - radio chirp played with every announcer subtitle
  - **Superweapon** - descending alert on launch
  - **HeroAbility** - rising three-note ability confirmation
- `AudioDirector.MakeSequence` builds decaying multi-note clips, so no imported audio
  assets are required.
- Out-of-range cue indices are ignored defensively.
- Added `AudioDirector.HasCue` and `ClipCount` for validation.

## Verification
- EditMode **111/111**
- PlayMode **90/90**
- Windows player built.

New coverage:
- the director creates a clip for every `AudioCue`
- the new acknowledgement, announcer and alert cues exist and play without throwing

## Known limits
- These are synthesised radio tones, not recorded voice lines.
- No per-unit voice variation, ducking or subtitle-to-audio sync beyond the chirp.
