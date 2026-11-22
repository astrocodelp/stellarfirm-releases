# Emberfall: Chronicle of the Hollow Crown — Game Design Document

v0.1 · 2026-08-14 · Owner: CEO · Engine: Unity 6 LTS (URP) · Platform: Steam (Windows first, Steam Deck Verified target; Linux/macOS after)

## 1. High concept

**A dark-fantasy action RPG where light is the only resource that matters.** Thirty years ago the sun was murdered — its heart split into seven crown-shards and buried in the Hollow beneath the capital of Veyr. You are the last Emberbearer: your body holds a dying coal. Every fire you light is a piece of your own life; every memory you recover rekindles it. Delve the shifting Hollow, reclaim the shards, and at the end choose what kind of sun rises — or whether one should.

**Pillars**
1. **Combat with weight and mercy** — readable, telegraphed, stamina-driven melee where a parry feels like a drumbeat. Hard but fair; accessibility options never gate content.
2. **Light as economy** — torch radius, enemy aggression, loot visibility and NPC memory all key off your Ember level. Spend light to buy, to heal, to open paths; hoard it and the dark hunts you.
3. **A world that remembers** — seeded procedural Hollow floors inside a hand-authored overworld; choices persist in the save and change NPC dialogue, prices and the ending.
4. **Builds, not spreadsheets** — six attributes, weapon classes with distinct movesets, 24 skills across 3 archetypes, affix-crafted gear. Depth you feel in your hands, not in a menu.

**Comparable feel:** Hades' combat clarity × Dark Souls' weight × Darkest Dungeon's light economy, in a 3D isometric world.

## 2. The player fantasy & loop

- **Minute loop:** enter room → read enemy telegraphs → fight (combo/dodge/parry/skill) → loot → decide: push deeper or bank embers at a shrine.
- **Hour loop:** clear a Hollow floor (5–9 rooms + elite + boss gate) → return to the Bastion hub → spend embers (level, craft, quest) → unlock a memory vignette → next floor mutates.
- **Campaign loop:** 7 shards ≈ 7 biomes ≈ 25–35 h main, 60+ h completionist. Each shard boss changes a world rule (e.g. the Shard of Tides makes water walkable).

## 3. Systems (v0.1 implemented in code)

### 3.1 Attributes & progression
STR, DEX, VIG, FOC, WIL, LCK (base 10, +3 points/level, cap 50). Derived: HP = 80 + 12·VIG + 2·level; Focus = 30 + 8·FOC; Stamina = 60 + 4·VIG + 2·WIL; physical power = weapon base ·(1 + 0.04·STR or DEX per weapon scaling); crit = 5% + 0.5%·LCK (cap 50%); ember capacity = 100 + 10·WIL. XP curve: `xpToNext(n) = 50·n + 25·n²` — level 2 costs 150, level 10 costs 3 000. All pure functions in `Progression.cs`, unit-tested.

### 3.2 Combat
Real-time, lock-on optional, 8-way dodge with 0.25 s i-frames (costs 20 stamina). Light combo: 3 hits, 0.55 s window between links, third hit +50% damage. Heavy: 2× damage, 35 stamina, 0.8 s windup (interruptible by stagger). Parry: 0.18 s window inside a held block; success staggers attacker 2 s and refunds focus. Every enemy attack has a **windup telegraph: a ground ring (shape) + the word "!" marker + audio sting** — never colour alone. Damage pipeline: base → attribute scaling → variance ±10% (seeded) → crit ×1.5 → armour reduction `dmg · 100/(100+armour)` → resistances. Pure logic in `CombatMath.cs` + `CombatState.cs` (stamina, combo window, i-frames — all testable without Unity physics).

### 3.3 Skills & archetypes
Emberknight (tank/fire), Ashdancer (mobility/crit), Hollowcaller (summons/dark). v0.1 ships 3 skills: **Ember Slash** (arc AoE, 15 focus), **Cinder Step** (teleport-dodge, 20 focus), **Ward of Cinders** (absorb shield, 25 focus). Skills are ScriptableObjects with cost, cooldown, effect enum — data-driven, no per-skill code forks.

### 3.4 Items, affixes, loot
Slots: Weapon, Helm, Chest, Gloves, Boots, Ring ×2, Amulet. Rarities: Common/Uncommon/Rare/Epic/Legendary with 0/1/2/3/4 affixes. Affix pool is data (flat HP, +%crit, +stamina regen, lifesteal…). Loot rolls are **seeded per room** so saves/tests reproduce them exactly (`LootSystem.cs`). Weapons define damage, speed, stamina cost, STR/DEX scaling letters (S–E). No inventory tetris: stackable consumables, auto-sort, compare tooltips.

### 3.5 The Hollow (procedural floors)
Seeded room-graph generator: entry → 4–8 rooms (combat 60%, treasure 15%, shrine 15%, elite 10%) → boss gate. Rooms pick encounter tables by biome + floor depth; difficulty scales `1 + 0.12·depth`. Deterministic given (runSeed, floorIndex) — the daily-run seed enables leaderboards later through the Steam seam. v0.1: generator + tests; graybox arena stands in for room geometry until the vertical slice.

### 3.6 Ember (light) economy
Ember 0–capacity. Drains 1/s in the Hollow; +5 per kill, +25 per shrine, +50 per boss. Below 25%: enemies gain +20% aggression, loot glow hides (item *names* always visible — accessibility). Spend ember at shrines to heal, at the Bastion to buy/craft. Death: lose half carried ember, recoverable at your death-mark (souls-like, but one death-mark only — mercy).

### 3.7 Quests & world memory
Quest definitions are ScriptableObjects: prerequisite quest ids, objective type (Kill/Collect/Talk/Reach), target id, count, rewards (XP, ember, item, flag). `QuestSystem.cs` is a pure state machine (Locked→Active→Completed→Rewarded) with event intake — unit-tested. World flags persist in the save and gate dialogue/endings.

### 3.8 Saves
Versioned JSON (`version: 1`) under `Application.persistentDataPath/saves/slot{n}.json`: attributes, XP, ember, inventory instance ids + rolled affixes, quest states, world flags, run seed, floor, settings. **Atomic write** (temp + replace), 3 slots + autosave, migration hook `SaveMigrations.Migrate(oldVersion, json)`. Steam Cloud comes later through `IPlatformServices` — the local save is always the truth.

## 4. Content plan (post-v0.1)

- **Biomes (7):** Sunken Archive, Root-Choir Forest, Glass Steppe, Drowned Belfry, Ashen Court, Starless Sea, The Crownroom. Each: 1 tileset, 6–8 enemy types, 1 elite, 1 shard boss with 3 phases.
- **Enemies v0.1:** Husk (melee, 3-hit), Cinder Wisp (ranged, kite), Hollow Knight (elite, parry-check). AI: simple state machine (Idle→Telegraph→Attack→Recover), no per-frame allocs.
- **NPCs/hub:** the Bastion — Smith (craft/reroll affixes), Archivist (quests/memories), Emberkeeper (level up), Cartographer (daily seeds). 12 named NPCs, all with remembered-state dialogue.
- **Narrative:** 40 memory vignettes (2–3 min, in-engine, skippable, subtitled), 3 endings driven by shard choices. Full VO deferred; all text localizable from day one (string tables, no hardcoded UI text).

## 5. Art & audio direction

Stylized painterly low-poly (readable silhouettes at isometric distance), 2-light scheme: cold ambient world vs warm ember key light on the player. URP + baked GI per room tile, one realtime light (your ember) — Steam Deck budget. Audio: adaptive layers (dark = sparse drones; high ember = strings), every telegraph has a distinct sting, full subtitle/caption support.

## 6. Accessibility (WCAG 2.2 AA applied to game UI)

Full keyboard + controller play with visible focus and rebindable inputs (Input System); text scale 100–200%; HUD meaning never colour-only (shapes + words); telegraphs have shape+sound+text; reduced-shake and reduced-flash settings (no flashes >3 Hz); hold-to-block has a toggle alternative; no button mashing anywhere; subtitles on by default with speaker names; colourblind-safe rarity icons (rarity also written as a word). Every menu reachable at 320 px-equivalent UI scale.

## 7. Tech architecture

- Assemblies: Runtime / Editor / EditMode tests / PlayMode tests. C# 9. No store SDK in gameplay — `IPlatformServices` seam (`NullPlatformServices` default; Steam implementation behind `STEAM_BUILD`).
- Data: ScriptableObject definitions (immutable) + plain-C# runtime state (serializable). Definitions referenced by string id in saves so asset GUIDs never leak into save files.
- Graybox first: `GrayboxBootstrap` builds a playable arena from primitives at runtime — the whole loop is testable before any art exists.
- Perf budget: 60 fps on Steam Deck at 800p, zero per-frame GC allocs in combat, object pools for projectiles/VFX/damage numbers.

## 8. What "best ever" honestly means

Nobody can promise the best RPG ever made — that's earned in reviews, not in a repo. What we can promise: a distinctive core (light-as-life), combat that feels world-class because it's tuned by playtest, zero accessibility gates, and a disciplined Steam launch. This document + this codebase is a real, buildable start on that path.
