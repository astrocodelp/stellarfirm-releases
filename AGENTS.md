# AGENTS

## Unity

- Unity 6.3 LTS (6000.x), Universal 3D (URP), Input System package, TextMeshPro. Checked 2026-08-14.
- Layout: everything the game owns lives under `Assets/_Project/` — `Scripts/Runtime`, `Scripts/Editor`, `Tests/EditMode`, `Tests/PlayMode`, `Art`, `Audio`, `Prefabs`, `Scenes`, `Settings`, `ScriptableObjects`. Third-party packs under `Assets/Plugins`. No `Assets/Resources`.
- Assemblies: `Emberfall.Runtime`, `Emberfall.Editor`, `Emberfall.Tests.EditMode`, `Emberfall.Tests.PlayMode`. Runtime references `Unity.InputSystem`, `Unity.TextMeshPro` (add `com.rlabrecque.steamworks.net` only after the package is installed).
- **C# 9 only** — no record types, no required members, no file-scoped namespaces from C# 10+.
- Gameplay rules: no store SDK calls outside `IPlatformServices` and the `#if STEAM_BUILD && !DISABLESTEAMWORKS` files; no per-frame allocations (no LINQ, no `new` in Update, cache components, `CompareTag`, `FindFirstObjectByType`); `Awaitable` with cancellation for async; saves are versioned JSON written atomically under `Application.persistentDataPath`; never hand-edit `.unity`, `.prefab`, `.asset` scene references or `.meta` files.
- No Unity Editor on the Coder's computer; CI runs the tests with GameCI; the CEO plays it.
- Never commit `.env`, keystores, `.ulf` files, `config.vdf` or any key.
