# HexPortal — Claude Code project memory

Mobile (Android-first, landscape) 1v1 turn-based fantasy card + tactics game on a 59-hex board, built in Unity 6.6 (6000.6.x, URP).
Core systems to keep in mind: two separate resources (**Mana** pays card costs, **Energy** pays unit actions), one action per unit per turn, fog of war with three visibility states, and a control zone that limits where cards can be played.
Team: 2 people (an analyst and a developer). Claude writes all code and architecture.

- **Talk to the user in Turkish.** Code, identifiers, comments and commit messages are in English.
- **Source of truth:** `docs/GDD.md`. Every rule has a stable ID (`U-04`, `C-17`, ...). The glossary in GDD §1 maps Turkish terms to code names; use those names.
- **Progress:** `docs/PROGRESS.md`. Read it first in every session, and update it when you finish a task.
- **Approvals:** Plans, GDD changes and Catalog numbers are approved by the `HexPortal PM` session via SendMessage. The user delegated design decisions to it.

@docs/GDD.md
@docs/PROGRESS.md

## Repository layout (target — create it in M0)

```
Assets/_Project/
  Core/            Pure C# rules engine. asmdef "HexPortal.Core", noEngineReferences: true
    Data/Catalog.cs  ALL numbers (units, cards, quests, passives, events, config). Must match the GDD exactly
  Game/            Unity client: views, input, UI, bootstrap. asmdef "HexPortal.Game" -> references Core
  Art/  Audio/     Assets only
Tools/
  Engine.Tests/    NUnit test project. Compiles ../../Assets/_Project/Core/**/*.cs directly. Runs without Unity
  Core.Build/      Build-only netstandard2.1 compile of Core (Unity's API surface); built by Engine.Tests
  Sim/             Console app. AI-vs-AI batch simulator for balance
docs/              GDD.md, PROGRESS.md
```

## Commands

- Run the tests (this is the main feedback loop): `dotnet test Tools/Engine.Tests --nologo`
- Run the simulation: `dotnet run --project Tools/Sim -- --games 1000 --seed 1`
- Always pass a project path to `dotnet`. The repo root also holds Unity-generated `.sln` and `.csproj` files.
- **Unity Editor (live):** Claude drives the open Editor via the Unity CLI (`unity:unity-cli` skill; package `com.unity.pipeline` 0.8.0-exp.1). After Unity-side changes or new files: `unity command eval "UnityEditor.AssetDatabase.Refresh();"`, then in a separate call `unity command recompile`, poll `recompile_status` until `completed`, then `console_status` must show `compilationFailed=false` and `consoleErrors=0`. Visual checks: `editor_play` → `capture_game_view` (landscape) → inspect the image → `editor_stop`. `unity test` (batch mode) cannot run while the Editor is open; use `run_tests`. Fallback when the CLI is not `ready`: `Library/ScriptAssemblies/HexPortal.*.dll` newer than the newest `.cs` and no `error CS` in `Logs/Editor.log` after the last compile.

## Working loop (every task)

1. Read `docs/PROGRESS.md` and the GDD rules you are about to touch.
2. For anything bigger than a small fix, write a short plan first and wait for the user's OK.
3. **Tests first.** For each rule, add a test named `RuleId_Behaviour`, for example `U09_RiderPassesThroughUnits`.
4. Implement it in the smallest way that makes the tests pass. No speculative abstractions.
5. `dotnet test` must be green. A Stop hook enforces this when Core or Tools files change.
6. Verify Unity-side changes yourself (see Commands). Ask the user only for real-device tests and look-and-feel judgments.
7. For a milestone or a large diff, run the `gdd-reviewer` agent and fix what it finds.
8. Update `docs/PROGRESS.md`. Commit with a message like `[M2] U-07 U-09 BFS movement and rider pass-through`.

## Hard rules

- **If the GDD is ambiguous, silent or contradicts itself, ask the user.** Do not invent a rule. Record the decision in the GDD changelog, with the user's approval.
- **Never change the GDD or the numbers in Catalog.cs without explicit approval from the user or the HexPortal PM session.** Balance ideas go to the user as proposals.
- **Rule IDs are never renumbered.** A removed rule is marked `KALDIRILDI`.
- **Core never references UnityEngine or UnityEditor**, and never uses `System.Random`, `DateTime.Now` or `Guid.NewGuid`. A PreToolUse hook blocks this.
- Use the language features that work in both Unity 6.6 and .NET: C# 9 and netstandard2.1. Do not use `record` or `init` accessors.
- **Keep it minimal.** Prefer one clear file over a framework. No DI containers, no ECS, no generic event buses beyond what the GDD needs.
- **Offline first.** Online is Faz 3 and out of scope. Keep hidden information behind `PlayerView` from day one, so a server can be added later without rewrites. With fog of war (GDD §11), hidden information includes the opponent's units outside your sight.
- **Editor automation safety:** `eval`/`eval_file` run only C# we wrote and read first; never trigger a domain reload inside `eval`; `git status` clean before any Editor-driven change and review `git diff` after. Never define `ENABLE_RUNTIME_PIPELINE`. Development Builds are gated until the M6 decision in PROGRESS.

## Specialists

- **Agents** (`.claude/agents/`):
  - `rules-engineer`: Core, Catalog, AI and Sim
  - `unity-client-dev`: everything under Game/
  - `gdd-reviewer`: read-only check of code against the GDD
  - `balance-analyst`: runs Sim and reports numbers, never edits them
- **Skills** (`.claude/skills/`):
  - `hex-grid`: coordinates, board layout, symmetry, movement BFS, ranges and visibility. Load it before any hex math.
  - `add-content`: checklist for adding a card, quest, passive or event
- **Path rules** (`.claude/rules/`): these load automatically for Core, Game and docs files.
