# Progress

Current milestone: **M2**

Milestone definitions and "done" criteria: GDD §17.

| Milestone | Status | Notes |
|---|---|---|
| M0 Skeleton | ✅ Done (2026-09-30) | 82 tests green; Unity 6000.6.3f1 compiles, Play shows "HexPortal M0". Commit 4c32ad7 |
| M1 Board | ✅ Done (2026-09-30) | 130 tests green, Unity compiles. Seeds 1–1000: 96.2% valid on first attempt, max 3 attempts, 100% distinct, mean cluster 4.03 |
| M2 Units | ⏳ Plan approved | |
| M3 Cards | ⏳ | |
| M4 Match | ⏳ | |
| M5 AI + Sim | ⏳ | |
| M6 Graybox client | ⏳ | |
| M7 Demo polish (Faz 2) | ⏳ | |

## Open items / blockers
- The template assets (`Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`) are still in the project; they can be deleted.
- Repo layout: the git root is the parent folder `Project-Card-Game/`. This Unity project is `HexPortal/`; `Project Card Game/` is the old 2022.3 prototype and is not used.

## Decisions
- 2026-09-30 — GDD v2.2 (B-22 seeds per biome + balanced growth, B-23 wellspring not next to a rune, B-24 retry continues the same RNG stream).
- 2026-09-30 — The map RNG is `new Rng(seed ^ MapStreamSalt)`; later streams (deal, draws) must use a different salt. `GameMap.Set` is internal (engine only).
- 2026-09-30 — B-05 has no M1 test: it is data-only (board data is never transformed per player); the 180° view is the camera in M6.
- Open for later: `TestBoard` builder arrives in M2 with units.
- 2026-09-30 — The main scene is `Assets/Scenes/Main.unity` (not under `_Project/`); it stays there. It is the only build scene; Portrait and Input System are set in Player Settings.
- 2026-09-29 — A new Unity 6.6 URP project in `HexPortal/` (no in-place upgrade of the 2022.3 prototype). Kit files live in `HexPortal/`.
- 2026-09-29 — GDD v2.1: character cards have no rarity (UX-07); a tower shot always deals 2 damage (U-27).
- 2026-09-29 — `Tools/Core.Build` (netstandard2.1) is built by `dotnet test`, so Core API use that Unity doesn't have fails the test run.

## Session log
<!-- Newest first. One line per session: date — what was done — what is next -->
- 2026-09-30 — M1 implemented (rules-engineer): Hex, Board, HexSearch (canPass/canStop), Rng (xorshift64*, unbiased), GameMap, MapGenerator, MapValidator; 130 tests green. gdd-reviewer: no blocking issues; fixed stream salt, internal Set, strict distinct test, retry-stream test, comments. Next: Unity check → M1 commit → M2 plan.
- 2026-09-30 — User verified M0 in Unity (no compile errors, label shows). M0 committed with .meta files (4c32ad7). Next: M1 plan.
- 2026-09-29 — Kit v2.1 installed in the new HexPortal Unity 6.6 project. M0: Core/Game asmdefs, Definitions.cs + Catalog.cs (all GDD v2.1 numbers), Engine.Tests (82 green: Catalog + architecture), Core.Build, Sim stub, runtime Bootstrap (portrait camera + UI Toolkit "HexPortal M0" label with Resources theme). gdd-reviewer: no blocking issues; the major finding (netstandard2.1 check) and the minor/nit findings are fixed.
