---
paths:
  - "Assets/_Project/Game/**"
  - "Packages/manifest.json"
---
# Unity client rules

- **The client is a view.** It sends commands to `Engine` and animates the returned `GameEvent`s. It holds no gameplay rules or numbers. If you need a rule check, ask `Engine.GetLegalCommands`.
- **Zero manual scene setup.** Bootstrap from code with `[RuntimeInitializeOnLoadMethod]`. One empty `Main` scene is enough. The camera, board, lights and UI are created at runtime. This keeps everything text-diffable and AI-editable.
- **UI:** UI Toolkit (UXML + USS files under `Game/UI/`). No uGUI prefabs.
- **Input:** Input System package with EnhancedTouch for touch, and a mouse fallback in the Editor. Touch targets are at least 48 dp (GDD UX-01). Drag-to-target for cards (UX-03).
- **Board visuals (graybox):** Build a procedural hex mesh per tile with a flat color per biome. Get positions from `HexLayout` (see the `hex-grid` skill). For player B, rotate the camera 180° (GDD B-05). Never mirror the data.
- **Fog of war (GDD UX-09):** Render only from `PlayerView`, never from the raw `GameState`. `Hidden` = dark cloud over the tile, `Explored` = desaturated tile with semi-transparent "ghost" units from the last-seen snapshot, `Visible` = full color.
- **Resources (UX-04):** Show Mana and Energy as two clearly different meters (color + icon).
- **Opponent's turn (UX-10, T-11…T-13):** Show a "Rakibin turu" banner, animate the opponent's visible actions live, keep the Market/blind-draw buttons active for the pre-pick (T-11), and allow tapping own units to see ranges and drawing plan arrows (client-only, cleared at turn start).
- **Hotseat (S-08):** Show the "Telefonu rakibine ver" screen between **every** turn and hide the board until the next player taps, because each player has their own fog.
- **Landscape only.** Auto-rotation between Landscape Left and Right, portrait disabled, reference resolution 1920×1080. Set PlayerSettings through the Unity CLI (reviewed eval) and mirror it in the runtime bootstrap.
- **Mobile performance:** No per-frame allocations in `Update`. Pool VFX and damage numbers. Target 60 fps on mid-range Android.
- **Async AI:** Run the AI turn step by step with a coroutine, so the UI stays responsive (GDD AI-05).
- **Claude verifies in the live Editor** via the Unity CLI (see CLAUDE.md → Commands): refresh → recompile → `console_status` clean. For visual changes, enter Play and use `capture_game_view` in a landscape resolution (e.g. 1920×1080, or 960×540 for quick checks), inspect the image, then stop Play. Report to the user only what needs a human: real Android device tests and look-and-feel. Never edit `.meta` files, `Library/` or `ProjectSettings/` by hand; Editor-driven settings changes go through CLI commands and are reviewed with `git diff` before commit.
