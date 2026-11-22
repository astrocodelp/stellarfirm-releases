# CI

The workflow in `.github/workflows/tests.yml` runs two jobs with GameCI on every pull request (and on demand via "Run workflow"):

1. **EditMode tests** — all pure-logic suites (combat math, combo/stamina timing, XP curve, ember state, loot rolls, inventory, dungeon determinism + connectivity, quests, save round-trip/migration/atomic writes).
2. **PlayMode smoke tests** — boots the whole graybox from code headlessly: world builds, enemies spawn, skills damage, kills grant XP/ember, save/load restores state, death drops an ember mark and respawn heals. Runs only after EditMode passes.

**Both use your GitHub Actions minutes.** Nothing runs until you add three repository secrets (GitHub → repo → Settings → Secrets and variables → Actions):

- `UNITY_LICENSE` — the contents of your Unity license file (GameCI's docs describe how to obtain a personal license `.ulf`; never paste it in chat or commit it).
- `UNITY_EMAIL`, `UNITY_PASSWORD` — the Unity account CI activates with.

## Local logic check without Unity (already green)

`tools/headless/run.sh` compiles `Core/` + the EditMode suites with mono (`mcs`) and runs them under a minimal NUnit shim — **62/62 passed** on the assistant's computer. It proves the game logic; it is not a substitute for the Unity compile + PlayMode run in CI (the shim only stands in for `JsonUtility`/`Application.persistentDataPath`). Requires `mono-mcs` / `mono-devel`.

Optional but recommended locally:
- `git lfs install` once on every machine that clones the repo (big art is stored with LFS).
- Unity Smart Merge as the git merger for `.unity/.prefab/.asset/.meta` (Unity's `UnityYAMLMerge` tool; Unity's docs explain the setup).

All `.meta` files are committed (deterministic GUIDs), so CI checkouts stay clean.

## Windows build

`.github/workflows/build-windows.yml` (manual: Actions → Build Windows → Run workflow) calls `Emberfall.Editor.BuildScript.BuildWindows`, which creates `Assets/Scenes/Main.unity` from code if missing, registers it as the boot scene and builds `Emberfall.exe` (Windows x64). The `Emberfall-Windows64` artifact on the run page is the playable game — unzip and run the exe; it boots straight into the graybox. Same three Unity secrets as tests. Both workflows fail fast with a named error if a secret is missing.
