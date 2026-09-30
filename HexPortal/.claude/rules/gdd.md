---
paths:
  - "docs/**"
---
# GDD editing rules

- Only edit `docs/GDD.md` after the user explicitly approves the change in this conversation.
- Never renumber rule IDs. New rules get the next free number in their section. Removed rules become `~~...~~ KALDIRILDI (vX.Y)`.
- Every change gets a line in the "Değişiklik Günlüğü" table, and bumps the version: minor for balance/numbers, major for a rule change.
- When a number changes, update `Core/Data/Catalog.cs` and the affected tests in the same commit.
- The GDD is written in Turkish. Keep code names in the glossary (§1) when adding new terms.
- `docs/PROGRESS.md` may be updated freely: status, blockers, session log.
