---
name: hex-grid
description: Exact hex math for HexPortal — board layout (GDD v3.0: radius-6 hexagon, 127 cells), axial coordinates, directions, distance, movement BFS (incl. Rider pass-through), ranges, visibility/fog and control zone, 180° symmetry, home zones, player halves and world positions. Load before writing or reviewing any coordinate, movement, range, visibility or map-generation code.
---
# HexPortal hex grid reference

These values were verified by tests (`BoardTests`, `UnitTests`, `MapGenTests`, `MapEventTests`). Do not re-derive them. Use them exactly as written here.

## Board shape (GDD v3.0, B-01…B-06)

The hexes are **pointy-top**. The board is a **hexagon of radius 6** around the Portal: **127 cells**.

Use **axial** coordinates `(q, r)` with the origin at the Portal. The cube coordinate is `s = -q - r`. There is no other storage coordinate system (the old doubled-width coordinates of the 59-cell board were removed in v3.0).

```
on board  <=>  max(|q|, |r|, |q + r|) <= 6          (Catalog.BoardRadius)
row r has q from max(-6, -6 - r) to min(6, 6 - r)
```

`Board.Cells` is in a fixed order: by `r` (top row first, `r = -6`), then by `q`. `Board.IndexOf(h)` is the position in that list (-1 off the board); per-cell arrays (`GameMap`, `FogMemory`, `PlayerProgress`) use it.

There are 13 rows. GDD rows are 1-based, row 1 at the top (player B's edge): `GddRow = r + 7`, `GddCol = q - max(-6, -6 - r) + 1` (1 = leftmost).

| Thing | Value |
|---|---|
| Row lengths (GDD rows 1..13) | 7, 8, 9, 10, 11, 12, **13**, 12, 11, 10, 9, 8, 7 |
| Portal (B-02) | `(0,0)` = GDD row 7, col 7 |
| Home zone B (top, B-03) | `r <= -5` (GDD rows 1–2) → 7 + 8 = **15** cells |
| Home zone A (bottom, B-03) | `r >= 5` (GDD rows 12–13) → **15** cells |
| Shortest distance home zone → Portal | **5** (e.g. `(0,-5)`, `(5,-5)`) |
| "Upper half" = B's half (B-21, B-06) | `r < 0`, or `r == 0 && q < 0` → **63** cells (GDD rows 1–6 + the 6 cells left of the Portal) |
| A's half (B-06) | the mirror of B's half: `r > 0`, or `r == 0 && q > 0` → **63** cells |
| Rune/Wellspring band, upper half (B-23) | `r ∈ {-4,-3,-2}` (GDD rows 3–5) → **30** cells. Mirror band: `r ∈ {2,3,4}` (GDD rows 9–11) |
| Mirror / pair cell (B-04) | `(q,r) → (-q,-r)`; GDD row n ↔ row 14 - n |
| Corners | `(0,-6) (6,-6) (6,0) (0,6) (-6,6) (-6,0)`; each has **3** on-board neighbours |
| Back-row centre (TestBoard default towers) | A `(-3,6)`, B `(3,-6)` |

Validity: `Board.IsOnBoard(h)` (a dictionary built once). `Board.AddWithin(set, center, radius)` adds the on-board ball around a cell in `Board.Cells` order (use it instead of scanning all 127 cells).

## Directions

```
Straight (6 edge neighbours), in axial (dq, dr), Hex.Directions order:
  (+1, 0) (+1,-1) (0,-1) (-1, 0) (-1,+1) (0,+1)
Distance:  (|dq| + |dr| + |dq+dr|) / 2
Ring of radius k around c: start at c + k*(-1,+1), then walk k steps along each direction 0..5.
```

There are no "diagonal" (vertex) directions in the rules any more (U-08 was removed in GDD v2.0). Straight lines are still used for the Archer's range and for Push (C-18).

## Movement (GDD U-07, U-09…U-12)

All units use **one** rule: a breadth-first search over edge neighbours.

- **Normal units:** BFS from the unit for up to `Move` steps. A step may enter a cell only if it is on the board, **empty** (no unit, tower or rock) and **Visible** to the moving player (U-12). Every reached cell (except the start) is a legal destination. You may end on the Portal.
- **Rider (U-09):** Same BFS, but it may pass **through** cells that hold a unit or tower (not rock). Those cells are not destinations. Path and destination cells must still be Visible (U-12).
- **Move bonus** (C-14) simply increases `Move` for the BFS.
- **Traps (C-32):** Only the cell where the unit **stops** can trigger a trap. Cells passed through never trigger. So the engine does not need to pick a path; the destination is enough.

Verified reach counts on an **empty, fully visible** board (= the on-board distance ball minus the start; `BoardTests.Hex_ReachCounts`):

| Start (q,r) | Where | Move 1 | Move 2 | Move 3 |
|---|---|---|---|---|
| (0,0) | Portal | 6 | 18 | 36 |
| (0,-6) | corner | 3 | 8 | 15 |
| (3,-6) | middle of the top edge | 4 | 11 | 21 |

Any middle-of-edge cell (e.g. `(6,-3)`) has the same counts as `(3,-6)`.

## Visibility (GDD V-01…V-10)

- A player's **Visible** set = all on-board cells within `Sight` distance of any of their units or their tower, plus the Portal (V-06), plus cells of enemy units that are currently revealed (V-08). No line-of-sight blocking.
- **Explored** is a per-player bit set that only grows. Initial value: the player's own half (B-06, 63 cells). The Portal belongs to neither half.
- Keep a per-player **last-seen snapshot** for each explored cell (terrain + what was on it). Update it for every cell that is Visible after each event (`Visibility.UpdateMemory`, index-based hot path).
- **Control zone (C-02):** cells at distance ≤ `ControlRange` (1) from any own unit or own tower. With every `Sight` ≥ 2, the control zone is always Visible.

## Ranges

- Melee: distance 1.
- **Archer:** Any cell along a straight direction at `k = 1..3`. **Nothing** in between blocks: units, towers and rocks are all ignored (U-03). A line that leaves the board never re-enters it (the board is convex), so stop at the first off-board cell.
- **Mage:** distance 1–2, any direction. The splash hits enemy units and the enemy tower on the target's 6 straight neighbours (U-04).
- **Tower (U-06, U-27):** distance 1–2.
- **Overwatch (U-28):** the unit's normal attack range.
- **Guardian cover (U-11):** When the target is adjacent (distance 1) to a Guardian of its own side and the target is **not a Guardian itself**, it cannot be chosen by an **attack** (this includes overwatch and tower shots). Guardians never receive cover, not even from another adjacent Guardian. A friendly tower adjacent to a Guardian is covered. Only Guardians that are **Visible to the attacking side** count (V-11); for tower shots and overwatch the attacking side is the shooter's owner.
- Every attack target must be **Visible** to the attacker's owner (V-07).

## Map generation and events (B-20…B-24, E-*)

- Generation works on the 63-cell upper half (B-21) and mirrors it. Seeds 1–1000 (`MapGenTests` stats): **98.3%** valid on the first attempt, max **3** attempts, 1000/1000 distinct maps; mean biome cluster **6.65** cells, mean largest cluster **16.7**, max cluster **25**.
- E-10/E-12 targets: mirror pairs outside both home zones with no marker. E-11: center at distance ≥ 3 from the Portal; a cluster (center + on-board neighbours) never overlaps its mirror (that needs distance ≤ 1). Minimum terrain-eligible targets over seeds 1–1000 (mirror pairs): **E-10 45, E-11 54, E-12 45**.

## World layout (Unity, `HexLayout`)

```
size   = hex outer radius (world units)
worldX = size * sqrt(3) * (q + r / 2f)
worldZ = -size * 1.5f * r      // r = -6 (B's edge) at the top of the screen (+Z) for player A's camera
```

Board extent (centre to centre): `width = 12 * sqrt(3) * size ≈ 20.8 * size` (row `r = 0`), `depth = 18 * size`. For player B's view, rotate the camera 180° around Y. Never transform the data.

## Must-have tests

- There are 127 cells, rows 7..13..7. Both home zones have 15 cells at distance ≥ 5 from the Portal. The upper half has 63 cells. The Portal is at (0,0).
- `Mirror(Mirror(h)) == h`, and `Mirror(h)` is on the board for every `h`.
- `Board.Cells` equals the axial hexagon `max(|q|,|r|,|q+r|) ≤ 6`, ordered by r then q.
- Both player halves (B-06) have 63 cells, they do not overlap, and together with the Portal they cover all 127 cells.
- Movement BFS on an empty, fully visible board matches the reach-count table above.
- Movement never passes through an occupied cell, except for the Rider; the Rider never passes through rock and never ends on an occupied cell.
- For 1000 seeds, the generated map is exactly symmetric and satisfies B-22 and B-23; every map-event type has a terrain-eligible target.
