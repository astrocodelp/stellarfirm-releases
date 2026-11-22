# Emberfall: Chronicle of the Hollow Crown

A dark-fantasy action RPG for Steam, built with **Unity 6 (6000.x LTS)**, Universal 3D (URP), the new Input System, and TextMeshPro.

You are the last Emberbearer, walking a kingdom where the sun went out a generation ago. Fire is currency, memory, and weapon. Delve the Hollow — a shifting dungeon beneath the dead capital — reclaim the seven crown-shards, and decide what kind of light comes back.

## What is in this repository (v0.1 "First Ember")

- **Tactical real-time combat** — stamina, 3-hit light combos, heavy attacks, dodge with i-frames, timed parry, focus-cost skills.
- **Build depth** — 6 attributes, derived stats, XP curve to level 50, 3 skills across archetypes, equipment with 5 rarities and rolled affixes, seeded loot tables.
- **Procedural dungeons** — deterministic seeded room-graph generator (entry → combat/treasure/shrine/elite → boss), fully unit-tested.
- **Quests** — data-driven quest state machine with prerequisites, counters, rewards.
- **Saves** — versioned JSON, atomic writes (temp file + replace), 3 slots, migration path from older versions.
- **Steam-ready architecture** — gameplay talks only to `IPlatformServices`; Steam code lives behind `STEAM_BUILD` guards, so the same code builds for itch.io/GOG/Epic later.
- **Accessibility (WCAG 2.2 AA intent for the game UI)** — full keyboard + controller play, text labels on every HUD element (no meaning by colour alone), telegraphed enemy attacks with a shape + word marker, settings for text scale, reduced shake and reduced flashing, no required button mashing.
- **Playable graybox** — add one component (`GrayboxBootstrap`) to an empty scene and press Play: arena, player, enemies, HUD, inventory, pause, death/respawn, save/load. No scene authoring needed to try the game.

## Open it (CEO steps, in order)

1. In Unity Hub, create a **Unity 6 LTS (6000.0.x), Universal 3D** project named `Emberfall` (company name: your studio name — decide now, save paths depend on it).
2. Clone this repo and open it directly in Unity Hub ("Add project from disk"). `Assets/Scenes/Main.unity`, `ProjectSettings/` and all `.meta` files are already committed — Unity imports and you are ready.
3. Nothing to install: `Packages/manifest.json` pulls Input System, URP and the test framework, and Active Input Handling is already set to **Both** in the committed Player Settings. (TextMeshPro/Cinemachine come later with real UI/cameras.)
4. Open `Assets/Scenes/Main.unity` and press **Play** — the `GrayboxBootstrap` on the `Emberfall` object builds the arena, player, waves, HUD and saves from code. That's it.
5. Optional, for designers: menu **Emberfall → Create Starter Content** generates editable ScriptableObjects (weapons, armour, enemies, skills, quests); drag the generated `ItemDatabase` into the bootstrap's Database field to override the code defaults. Menu **Emberfall → Create Playable Scene** regenerates `Main.unity` if you ever delete it.
6. **File → Build Profiles → Windows → Build** produces `Emberfall.exe`, which boots straight into the game (scene already registered).

## Controls (graybox defaults; the Gamepad column is the planned Input System mapping)

| Action | Keyboard/Mouse | Gamepad |
| --- | --- | --- |
| Move | WASD | Left stick |
| Light attack | Left click / J | X / Square |
| Heavy attack | Right click / K | Y / Triangle |
| Dodge | Space | B / Circle |
| Block / Parry | Shift (hold) | LB / L1 |
| Skills 1–3 | 1 / 2 / 3 | D-pad + RB |
| Interact | E | A / Cross |
| Inventory | Tab | Select |
| Attributes | C | — |
| Save / Load | F5 / F9 | — |
| Pause | Esc | Start |

## Tests & CI

EditMode tests (62) cover combat math, XP curve, ember state, loot rolls, inventory/equipment, dungeon determinism and connectivity, quest flow, save migration/atomic writes and combo timing — **all 62 pass**, verified headlessly on the assistant's computer via `tools/headless/run.sh` (mono; the pure core compiles and runs with no Unity). PlayMode smoke tests boot the whole graybox headlessly and check the loop end to end. `.github/workflows/tests.yml` runs both with GameCI on every pull request — it uses your GitHub Actions minutes and needs `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets (see `docs/ci.md`). The Unity-side compile and the playable feel still need the Editor or CI (no Unity Editor on the assistant's computer).

## Roadmap to Steam

See `docs/GDD.md` (full design) and `docs/roadmap.md` (vertical slice → content → Steam launch plan, including the Steamworks fee, reviews and fixed waits). Steam publishing, prices and the store page are the CEO's clicks; the code is ready for the store seam.
