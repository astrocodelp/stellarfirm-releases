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
2. Copy this repository's `Assets/`, `ProjectSettings/` additions and docs over the fresh project (or clone this repo and open it — Unity will generate `ProjectSettings` on first open; set serialization to Force Text and Visible Meta Files in Editor settings).
3. Package Manager: install **Input System**, **TextMeshPro** (import TMP Essentials when prompted), **Cinemachine** (optional). Set Active Input Handling to **Both** (Player Settings → Other Settings). The graybox uses the legacy Input API so it is playable immediately; the shipping rebinding UI moves to the Input System asset.
4. Open the project once, let it import, and **commit the generated `.meta` files**.
5. Menu **Emberfall → Create Starter Content** — generates all starter ScriptableObjects (weapons, affixes, enemies, skills, quests, database, loot tables).
6. Create a scene `Assets/_Project/Scenes/Game.unity`, add an empty GameObject `Bootstrap`, add the **GrayboxBootstrap** component, drag the generated `ItemDatabase` into its Database field (and optionally the `Emberfall` input asset later). Press **Play**.
7. Add the scene under **File → Build Profiles**.

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
