# Progress

Current milestone: **M4**

Milestone definitions and "done" criteria: GDD §17.

| Milestone | Status | Notes |
|---|---|---|
| M0 Skeleton | ✅ Done (2026-09-30) | 82 tests green; Unity 6000.6.3f1 compiles, Play shows "HexPortal M0". Commit 4c32ad7 |
| M1 Board | ✅ Done (2026-09-30) | 130 tests green, Unity compiles. Seeds 1–1000: 96.2% valid on first attempt, max 3 attempts, 100% distinct, mean cluster 4.03 |
| M2 Units | ✅ Done (2026-09-30) | 197 tests green. Movement, combat, cover (V-11), tower shot, overwatch, Energy/actions, V-02/V-07, W-02, replay + random legal/illegal command tests |
| M3 Cards | ✅ Done (2026-09-30) | 287 tests green, Unity compiles. Mana, Control Zone, deploy, 12 support cards, traps, pools/Market/hands, mandatory first draw, T-11 pre-pick (engine), U-23, U-27 deploy trigger, U-29 trap-before-shots. GDD v2.7 |
| M4 Match | ⏳ | |
| M5 AI + Sim | ⏳ | |
| M6 Graybox client | ⏳ | |
| M7 Demo polish (Faz 2) | ⏳ | |

## Handoff (2026-09-30)

**State:** M0–M3 done. GDD v2.7. `dotnet test Tools/Engine.Tests --nologo` → 287 green. The Unity CLI bridge (`com.unity.pipeline` 0.8.0-exp.1) drives the open Editor; the game is landscape (GDD v2.5).

**Next work: M4 (Match)** — plan to HexPortal PM first. Scope per GDD §17: setup (S-*), fog/visibility + `PlayerView` and per-player event filtering (V-*), quests (Q-*), passives (P-*), map events (E-*), win conditions W-01/W-03/W-04. Hooks and notes from M3:
- Setup must call `Pools.Deal`, `Pools.OpenMarket`, then `Turn.StartTurn` (A's first turn draws, T-04 v2.6).
- `Rules/Passives.cs` has no-op hooks for P-02 (trap +1) and P-05 (market bonus); P-01, P-03, P-04, P-06 are new. P-06: a pushed or teleported unit also "enters" the Portal (every arrival counts, v2.7).
- C-35: `Traps.RemoveAt` exists for E-10; the `TrapRemoved` event must be shown to the trap's OWNER only (v2.7).
- Hidden info already in GameState per player: hands, pool order, Market, traps, pre-picks, match Rng. `TrapPlaced`/`CardDrawn` etc. are unfiltered until PlayerView.
- Q-12 kill credit: `UnitDied.Killer` is set for attack, splash, tower, overwatch, trap and poison kills.
- `TestBoard` starts with empty pools/Market/hands; use `FullPools()` for draw scenarios.

**Working rules:**
- Plan approvals and GDD decisions come from the "HexPortal PM" session via SendMessage. GDD/skill updates arrive as `C:\Users\Dev\Documents\GDD.md` + `HexPortal-kit.zip`; copy them, check the diff, and commit separately as `[docs] ...`.
- Flow per milestone: plan → approval → tests first → implement (rules-engineer) → `dotnet test` green → gdd-reviewer → PROGRESS → Unity check → commit → summary to PM and the user.
- **Unity check:** see CLAUDE.md → Commands (`hexportal_refresh` → `recompile` → `console_status` clean). New `.cs` files get their `.meta` from Unity automatically; commit them.
- Git identity is `ehza1` (repo-local config, set by the user); never change git config. No push. Python is not installed on this machine.

**Open notes:**
- U-28 "görüşünde" is implemented as the owner's Visible set (V-11). With the current Catalog every unit's range is within its own Sight, so both readings behave the same. No GDD change for now; revisit if Sight or range values change.
- The main scene is `Assets/Scenes/Main.unity` (not under `_Project/`); it stays there.

## Open items / blockers
- The template assets (`Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`) are still in the project; they can be deleted.
- Repo layout: the git root is the parent folder `Project-Card-Game/`. This Unity project is `HexPortal/`; `Project Card Game/` is the old 2022.3 prototype and is not used.

- **M6 GATE (pipeline in dev builds):** A Development Build defines `ENABLE_PROFILER`, so the `com.unity.pipeline` runtime (C# interpreter + server) is compiled into the player. Before the first Android build in M6, settle: (a) is that server reachable over the network on the device? (b) exclude it with an Editor build script, or use dev builds only for USB/local testing? No Development Build is distributed until this is decided.

## Decisions
- 2026-09-30 — Unity CLI allowlist (user-approved): `.claude/settings.json` allows only read-only/reversible verification commands (status, hexportal_refresh, recompile(_status), console(_status), editor_status/play/stop, capture_game_view, get_player_settings, list_tests, run_tests, test_status). `eval`/`eval_file` always need approval. `hexportal_refresh` lives in `Assets/_Project/Editor` (asmdef HexPortal.Editor, Editor only). New files get their `.meta` via refresh without a user click (verified). Game view is set to a 1920×1080 fixed size for captures (Editor-local state, not in git).
- 2026-09-30 — GDD v2.5: landscape orientation. PlayerSettings: AutoRotation, Landscape Left/Right only (a28e36f). The earlier "Portrait" setting and the Main.unity build scene had never been saved to disk; both are now persisted.
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
- 2026-09-30 — (Dev 2) M3 implemented (rules-engineer, 88 new tests), GDD v2.6/v2.7 clarifications (S1–S8, A1–A7, Mirror → no shots). gdd-reviewer: no blocking; major (Mirror-teleported unit drew overwatch) fixed with two U-29 tests; nits: unused `ArrivalKind` parameter kept as the U-27 hook, redundant Visible checks in Control Zone kept as a guard. 287 green, Unity compile clean.
- 2026-09-30 — (Dev 2) Process docs moved to live Editor verification (3510e35), GDD v2.5 landscape (ee21bba), landscape switch + AI leftover cleanup (a28e36f), CLI allowlist + hexportal_refresh. Next: M3 plan.
- 2026-09-30 — (Dev 2) Unity CLI bridge: AI packages removed, `com.unity.pipeline` 0.8.0-exp.1 installed (35f9d3a). Smoke test, Editor unfocused the whole time: refresh (`eval AssetDatabase.Refresh()`) + `recompile`/`recompile_status` pick up disk edits and compile without a user click (verified with a temporary comment in Bootstrap.cs, then reverted); `console_status` (groundTruth counts, compilationFailed) works, `console` entry buffer only holds logs from after the session starts; `eval` works; `editor_play`/`editor_stop` work, UIDocument label reads "HexPortal M0"; `capture_game_view` includes UI (`screenshot` renders the camera only, no overlay UI); `list_tests`/`run_tests` work (0 Unity tests in the project; `unity test` can't run while the Editor has the project open); `get_player_settings` + eval read Portrait, activeInputHandler=1 (New), IL2CPP, Android. From now on the Unity compile check is: `recompile` → `recompile_status` = completed → `console_status` compilationFailed=false, consoleErrors=0.
<!-- Newest first. One line per session: date — what was done — what is next -->
- 2026-09-30 — M2 implemented (rules-engineer) + V-11 change; gdd-reviewer two rounds, no blocking issues. 197 tests green. Unity plugin (unity-cli) not visible in this session. Next: M3 (cards) plan.
- 2026-09-30 — M1 implemented (rules-engineer): Hex, Board, HexSearch (canPass/canStop), Rng (xorshift64*, unbiased), GameMap, MapGenerator, MapValidator; 130 tests green. gdd-reviewer: no blocking issues; fixed stream salt, internal Set, strict distinct test, retry-stream test, comments. Next: Unity check → M1 commit → M2 plan.
- 2026-09-30 — User verified M0 in Unity (no compile errors, label shows). M0 committed with .meta files (4c32ad7). Next: M1 plan.
- 2026-09-29 — Kit v2.1 installed in the new HexPortal Unity 6.6 project. M0: Core/Game asmdefs, Definitions.cs + Catalog.cs (all GDD v2.1 numbers), Engine.Tests (82 green: Catalog + architecture), Core.Build, Sim stub, runtime Bootstrap (portrait camera + UI Toolkit "HexPortal M0" label with Resources theme). gdd-reviewer: no blocking issues; the major finding (netstandard2.1 check) and the minor/nit findings are fixed.
