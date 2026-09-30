# Progress

Current milestone: **M0** (code done; waiting for the Unity compile check and the first commit)

Milestone definitions and "done" criteria: GDD §17.

| Milestone | Status | Notes |
|---|---|---|
| M0 Skeleton | 🟡 Code done, `dotnet test` green (82) | Waiting for: Unity compile check by the user, git identity for commit |
| M1 Board | ⏳ | |
| M2 Units | ⏳ | |
| M3 Cards | ⏳ | |
| M4 Match | ⏳ | |
| M5 AI + Sim | ⏳ | |
| M6 Graybox client | ⏳ | |
| M7 Demo polish (Faz 2) | ⏳ | |

## Open items / blockers
- Git identity (user.name / user.email) is not configured on this machine. The user decides; set it with `git config --local` only.
- Unity manual steps (Claude cannot open Unity): empty `Assets/_Project/Scenes/Main.unity` as the only build scene, Player Settings > Default Orientation = Portrait, Active Input Handling = Input System Package. Commit the `.meta` files Unity generates for `Assets/_Project/**`.
- The template assets (`Assets/TutorialInfo`, `Assets/Readme.asset`, `SampleScene`) are still in the project. They can be deleted once `Main.unity` exists.
- Repo layout: the git root is the parent folder `Project-Card-Game/`. This Unity project is `HexPortal/`; `Project Card Game/` is the old 2022.3 prototype and is not used.

## Decisions
- 2026-09-29 — A new Unity 6.6 URP project in `HexPortal/` (no in-place upgrade of the 2022.3 prototype). Kit files live in `HexPortal/`.
- 2026-09-29 — GDD v2.1: character cards have no rarity (UX-07); a tower shot always deals 2 damage (U-27).
- 2026-09-29 — `Tools/Core.Build` (netstandard2.1) is built by `dotnet test`, so Core API use that Unity doesn't have fails the test run.

## Session log
<!-- Newest first. One line per session: date — what was done — what is next -->
- 2026-09-29 — Kit v2.1 installed in the new HexPortal Unity 6.6 project. M0: Core/Game asmdefs, Definitions.cs + Catalog.cs (all GDD v2.1 numbers), Engine.Tests (82 green: Catalog + architecture), Core.Build, Sim stub, runtime Bootstrap (portrait camera + UI Toolkit "HexPortal M0" label with Resources theme). gdd-reviewer: no blocking issues; the major finding (netstandard2.1 check) and the minor/nit findings are fixed. Next: user checks Unity, then commit, then M1.
