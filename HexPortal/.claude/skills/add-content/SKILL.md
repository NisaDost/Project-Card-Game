---
name: add-content
description: Checklist for adding or changing a unit class, support card, quest, commander passive or map event in HexPortal (GDD row → Catalog.cs → effect code → tests → sim). Use whenever content is added, removed or rebalanced.
---
# Adding or changing content

Content changes touch 4 places. All of them must stay in sync in **one commit**.

0. **Approval.** Get the user's explicit OK for the design, including the exact numbers. If the idea came from `balance-analyst`, show the proposal first.
1. **GDD** (`docs/GDD.md`):
   - Add a row in the right table with the **next free ID**: units U-0x, cards C-1x/C-2x, quests Q-1x, passives P-0x, events E-1x. Never reuse an ID.
   - Update the counts that depend on it: pool sizes in §4.2 and §5, and dealing rules in D-03 and D-04. **Card totals must stay even** so they split between 2 players.
   - Add a changelog line and bump the version.
2. **Catalog** (`Assets/_Project/Core/Data/Catalog.cs`):
   - Add one entry, with its `Id` string equal to the GDD ID. Cards and units have a Mana `Cost`; units also have `Sight`.
   - Use an existing `EffectKind` if one fits. Add a new one only if the effect is new.
3. **Effect code** (only if there is a new `EffectKind`):
   - Add one `case` in the effect switch.
   - Legal targeting goes in `GetLegalCommands`.
   - Hidden info goes through `PlayerView`, for traps and anything else secret.
4. **Tests:**
   - Add at least one test named `<ID>_...` for the effect.
   - Add a test for any limit it interacts with: the 1 buff + 1 debuff limit C-05 (new effect replaces the old one), the control zone C-02, the hand limit D-07, the Mana cap T-01.
5. **Verify:**
   - Run `dotnet test Tools/Engine.Tests --nologo`.
   - Run a quick sim: `dotnet run --project Tools/Sim -- --games 300 --seed 1`. Check that it doesn't crash and that the item gets used.
   - Run the `gdd-reviewer` agent on the diff.
6. **Client:** A new card needs an icon name in the client's icon map. Until the art exists, use a placeholder color and a letter.

Removing content: mark the GDD row `KALDIRILDI`, delete the Catalog entry and its tests, and keep the ID reserved.
