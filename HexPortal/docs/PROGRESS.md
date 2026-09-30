# Progress

Current milestone: **M3**

Milestone definitions and "done" criteria: GDD §17.

| Milestone | Status | Notes |
|---|---|---|
| M0 Skeleton | ✅ Done (2026-09-30) | 82 tests green; Unity 6000.6.3f1 compiles, Play shows "HexPortal M0". Commit 4c32ad7 |
| M1 Board | ✅ Done (2026-09-30) | 130 tests green, Unity compiles. Seeds 1–1000: 96.2% valid on first attempt, max 3 attempts, 100% distinct, mean cluster 4.03 |
| M2 Units | ✅ Done (2026-09-30) | 197 tests green. Movement, combat, cover (V-11), tower shot, overwatch, Energy/actions, V-02/V-07, W-02, replay + random legal/illegal command tests |
| M3 Cards | ⏳ | |
| M4 Match | ⏳ | |
| M5 AI + Sim | ⏳ | |
| M6 Graybox client | ⏳ | |
| M7 Demo polish (Faz 2) | ⏳ | |

## Handoff (2026-09-30, to a new Dev session)

**State:** M0, M1 and M2 are done. GDD v2.4. `dotnet test Tools/Engine.Tests --nologo` → 197 green (Core also builds for netstandard2.1 via Tools/Core.Build). Latest commits:
- `97f288f` [M2] Units: movement, combat, tower defense, overwatch, energy/actions, visibility, W-02
- `9a26429` [docs] GDD v2.4 · `b5bb5b1` [docs] GDD v2.3 · `9524641` [M1] Board · `3a0d97b` [docs] GDD v2.2 · `4c32ad7` [M0] Skeleton

**Uncommitted, not ours:** `Packages/manifest.json` and `packages-lock.json` add `com.unity.ai.assistant` 2.20.0-pre.1 and `com.unity.ai.inference` 2.6.1, and there is a new file `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json`. These appeared while the Unity plugin was being installed. The M2 commit leaves them out. Ask HexPortal PM whether to commit them as the `[tools]` package commit.

**Next work:**
1. ✅ DONE (see Session log). Unity plugin smoke test (unity-cli skill). Before any action that drives the Editor, `git status` must be clean. Do not change scenes or settings during the test. Report each item as works / fails / not supported:
   a. Asset refresh and triggering a compile
   b. Reading Console errors and warnings
   c. Running a small C# snippet in the Editor (e.g. return `Application.unityVersion`)
   d. Entering and leaving Play mode; while in Play, confirm the "HexPortal M0" Label text is in the UIDocument
   e. Game view screenshot (save it, report the path)
   f. Running Unity Test Framework EditMode tests (whether it is supported is enough for now)
   g. Reading Player settings (Default Orientation, Active Input Handling), read only
   If the plugin adds a bridge package, commit it separately: "[tools] Unity CLI editor bridge package".
2. Then the M3 (Cards) plan, sent to HexPortal PM for approval. Proposed scope:
   - C-01…C-06, C-10…C-21, C-30…C-35; D-01…D-07
   - T-01…T-03, T-06, T-07, and the engine part of T-11 (pre-pick command)
   - U-23 card draw; U-27 "deployed" trigger (`ArrivalKind.Deployed` hook exists); U-28 overwatch broken by push/teleport (`Defense.BreakOverwatch` hook exists)
   - The match Rng uses its own salt, separate from the map stream (`MapGenerator` uses `seed ^ MapStreamSalt`)
   - Pool order is hidden information, ready for M4's PlayerView
   - Debuff targets follow V-07 and V-11
   - Trap arrivals by push/teleport are trap-specific only (C-32); towers and overwatch ignore them (v2.4)

**Working rules:**
- Plan approvals and GDD decisions come from the "HexPortal PM" session via SendMessage. GDD/skill updates arrive as `C:\Users\Dev\Documents\GDD.md` + `HexPortal-kit.zip`; copy them, check the diff, and commit separately as `[docs] ...`.
- Flow per milestone: plan → approval → tests first → implement (rules-engineer) → `dotnet test` green → gdd-reviewer → PROGRESS → Unity check → commit → summary to PM and the user.
- **Unity compile check** until the plugin works: `Library/ScriptAssemblies/HexPortal.*.dll` must be newer than the newest `.cs` under `Assets/_Project`, and `Logs/Editor.log` must have no `error CS` after the last compile. Every new file needs its `.meta` (Unity makes them; never write .meta by hand). If they are missing, ask PM to have the user click Unity once.
- Git identity is `ehza1` (repo-local config, set by the user); never change git config. No push. Python is not installed on this machine.

**Open notes:**
- U-28 "görüşünde" is implemented as the owner's Visible set (V-11). With the current Catalog every unit's range is within its own Sight, so both readings behave the same. No GDD change for now; revisit if Sight or range values change.
- The main scene is `Assets/Scenes/Main.unity` (not under `_Project/`); it stays there.

## Open items / blockers
- The template assets (`Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`) are still in the project; they can be deleted.
- Repo layout: the git root is the parent folder `Project-Card-Game/`. This Unity project is `HexPortal/`; `Project Card Game/` is the old 2022.3 prototype and is not used.

- **M6 GATE (pipeline in dev builds):** A Development Build defines `ENABLE_PROFILER`, so the `com.unity.pipeline` runtime (C# interpreter + server) is compiled into the player. Before the first Android build in M6, settle: (a) is that server reachable over the network on the device? (b) exclude it with an Editor build script, or use dev builds only for USB/local testing? No Development Build is distributed until this is decided.

## Decisions
- 2026-09-30 — Security: `unity command eval` / `eval_file` only run C# we wrote and read before the command. Every change made through the Editor is reviewed with `git diff` before a commit.
- 2026-09-30 — Unity CLI bridge: `com.unity.pipeline` pinned at 0.8.0-exp.1 (the AI Assistant/Inference packages were removed; not needed). Release player builds only get `Unity.Pipeline.Attributes` (harmless attributes); the other runtime assemblies are constrained to `UNITY_EDITOR || ENABLE_PROFILER || ENABLE_RUNTIME_PIPELINE`. `ENABLE_RUNTIME_PIPELINE` is NEVER defined in this project.
- 2026-09-30 — GDD v2.2 (B-22 seeds per biome + balanced growth, B-23 wellspring not next to a rune, B-24 retry continues the same RNG stream).
- 2026-09-30 — The map RNG is `new Rng(seed ^ MapStreamSalt)`; later streams (deal, draws) must use a different salt. `GameMap.Set` is internal (engine only).
- 2026-09-30 — B-05 has no M1 test: it is data-only (board data is never transformed per player); the 180° view is the camera in M6.
- 2026-09-30 — GDD v2.3 (U-03 nothing blocks archer lines, U-05 heals stack, U-11 Guardians never covered) and v2.4 (V-11 visible-information principle: cover only from Guardians Visible to the attacking side; push/teleport never trigger shots).
- 2026-09-30 — M2 architecture: Engine.Apply validates with the same functions as GetLegalCommands; illegal commands throw without changing state. State setters are internal (tests compile Core sources). Tower shots and overwatch use the shooter owner's Visible set.
- 2026-09-30 — Unity check: Logs/Editor.log must show a compile after the last Core edit with no "error CS", and every new file needs its .meta before a commit.
- 2026-09-30 — The main scene is `Assets/Scenes/Main.unity` (not under `_Project/`); it stays there. It is the only build scene; Portrait and Input System are set in Player Settings.
- 2026-09-29 — A new Unity 6.6 URP project in `HexPortal/` (no in-place upgrade of the 2022.3 prototype). Kit files live in `HexPortal/`.
- 2026-09-29 — GDD v2.1: character cards have no rarity (UX-07); a tower shot always deals 2 damage (U-27).
- 2026-09-29 — `Tools/Core.Build` (netstandard2.1) is built by `dotnet test`, so Core API use that Unity doesn't have fails the test run.

## Session log
- 2026-09-30 — (Dev 2) Unity CLI bridge: AI packages removed, `com.unity.pipeline` 0.8.0-exp.1 installed (35f9d3a). Smoke test, Editor unfocused the whole time: refresh (`eval AssetDatabase.Refresh()`) + `recompile`/`recompile_status` pick up disk edits and compile without a user click (verified with a temporary comment in Bootstrap.cs, then reverted); `console_status` (groundTruth counts, compilationFailed) works, `console` entry buffer only holds logs from after the session starts; `eval` works; `editor_play`/`editor_stop` work, UIDocument label reads "HexPortal M0"; `capture_game_view` includes UI (`screenshot` renders the camera only, no overlay UI); `list_tests`/`run_tests` work (0 Unity tests in the project; `unity test` can't run while the Editor has the project open); `get_player_settings` + eval read Portrait, activeInputHandler=1 (New), IL2CPP, Android. From now on the Unity compile check is: `recompile` → `recompile_status` = completed → `console_status` compilationFailed=false, consoleErrors=0.
<!-- Newest first. One line per session: date — what was done — what is next -->
- 2026-09-30 — M2 implemented (rules-engineer) + V-11 change; gdd-reviewer two rounds, no blocking issues. 197 tests green. Unity plugin (unity-cli) not visible in this session. Next: M3 (cards) plan.
- 2026-09-30 — M1 implemented (rules-engineer): Hex, Board, HexSearch (canPass/canStop), Rng (xorshift64*, unbiased), GameMap, MapGenerator, MapValidator; 130 tests green. gdd-reviewer: no blocking issues; fixed stream salt, internal Set, strict distinct test, retry-stream test, comments. Next: Unity check → M1 commit → M2 plan.
- 2026-09-30 — User verified M0 in Unity (no compile errors, label shows). M0 committed with .meta files (4c32ad7). Next: M1 plan.
- 2026-09-29 — Kit v2.1 installed in the new HexPortal Unity 6.6 project. M0: Core/Game asmdefs, Definitions.cs + Catalog.cs (all GDD v2.1 numbers), Engine.Tests (82 green: Catalog + architecture), Core.Build, Sim stub, runtime Bootstrap (portrait camera + UI Toolkit "HexPortal M0" label with Resources theme). gdd-reviewer: no blocking issues; the major finding (netstandard2.1 check) and the minor/nit findings are fixed.
