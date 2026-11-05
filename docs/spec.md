# CQ R-A Revival — v1 Specification (frozen)

Original 3D real-time strategy. Genre-faithful to classic C&C, with no trademarks,
names, or assets from any existing title.

## Technical

- **Unity 6.3 LTS** (`6000.3.28f1`), URP 17.3, Entities 1.5, Burst 1.8.31
- Windows only. No macOS/Linux targets.
- Code-first: one bootstrap scene, content authored as plain-text JSON compiled into
  BlobAssets at load. Procedurally generated meshes — no external art dependency.
- **Deterministic fixed-timestep simulation at 30 Hz**, single-threaded (Burst-compiled).
  Parallelism is limited to per-destination flow fields and rendering.
- Flow-field pathfinding + spatial-hash steering and local avoidance.
  **No Unity Physics for units. No NavMesh.**
- Every player action is a serialisable command → replays, save/load, AI control.

## Game

- Classic base model: MCV deploy, free placement inside build radius, power grid, ore economy.
- One full player faction at first; 3 factions at content completion.
- Systems: veterancy · captureable neutral structures · garrisonable buildings ·
  engineers · transports · defensive structures + walls · research upgrades · crates ·
  repair & sell · stealth + detection · tech capture · directional armour ·
  terrain line-of-sight · destructible bridges.
- 4 difficulty tiers: Recruit / Veteran / Hardened / Brutal.
  **Pure skill scaling — no economy bonuses, no fog-of-war cheating, no stat changes.**
  All AI parameters live in `Assets/Data/ai/*.json`.

## Modes

- **Campaign** — 12+ missions across 3 factions, persistent hero units, branching
  missions, interactive briefings, codex entries.
- **Skirmish** — 1–8 players, free-for-all and teams (2v2–4v4), hand-authored JSON maps,
  mutators, match settings saved as JSON.
- **AI-vs-AI observer** — development and balance tool.

## Presentation

- Post-collapse industrial art direction. Rust / bone / ochre / copper / tar palette.
  Harsh low sun, dust haze. Team colour as accent stripes, never full recolour.
- Real-world scale: infantry 1.2 m, MBT 7 m, refinery 40 m.
- Procedural SFX synthesis. Layered soundtrack with drop-in `.ogg` override slots.
- Synthesised announcer + unit acknowledgements (SAPI), radio-filtered, with subtitles.
- War-room post-match graphs, replay browser with scrubbing, tread marks,
  construction animations, photo mode.

## The hook

**The AI plays by exactly the same rules as the player.** No bonuses, no vision cheats,
no hidden stat modifiers. This is provable via the replay browser's AI-vision overlay,
which shows precisely what each AI knew at any tick.

## Scaffolding requirements (built in Phase 1, non-negotiable)

- String tables for all UI text (`Assets/Data/strings/*.json`)
- Mods folder + content manifest + load order (`Assets/Mods/`)
- Versioned save/load serialisation
- Command-stream architecture
- Deterministic entity ID allocation
- Content validation CLI (fails the build on broken references)
- Golden replay regression tests

## Out of scope for v1

Dynamic campaign map · faction-specific economies · supply/logistics · weather ·
infantry terrain cover · superweapons · naval · map editor · Steam · multiplayer.
