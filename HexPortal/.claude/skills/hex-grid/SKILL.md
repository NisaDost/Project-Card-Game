---
name: hex-grid
description: Exact hex math for HexPortal — board layout (59 cells), coordinate systems, directions, distance, movement BFS (incl. Rider pass-through), ranges, visibility/fog and control zone, 180° symmetry, home zones, player halves and world positions. Load before writing or reviewing any coordinate, movement, range, visibility or map-generation code.
---
# HexPortal hex grid reference

These values were verified with a script. Do not re-derive them. Use them exactly as written here.

## Board shape (GDD B-01…B-05)

The hexes are **pointy-top**. There are 9 rows, `y = 0..8`, with **row 0 at the top** (player B's side). The GDD uses 1-based rows, so GDD row n = `y = n-1`.

Storage uses **doubled-width** coordinates `(x, y)`:
- Even `y` has 7 cells: `x ∈ {0,2,4,6,8,10,12}`
- Odd `y` has 6 cells: `x ∈ {1,3,5,7,9,11}`
- Total **59** cells. `x + y` is always even.

For all math, use **axial** coordinates `(q, r)` with the origin at the Portal. The cube coordinate is `s = -q - r`.

```
r = y - 4
q = ((x - 6) - r) / 2        // exact integer division, never fractional
// inverse:
y = r + 4
x = 2*q + r + 6
```

| Thing | Value |
|---|---|
| Portal (B-02) | `(q,r) = (0,0)` = doubled `(6,4)` |
| Home zone A (bottom) | `y ∈ {7,8}` → 13 cells |
| Home zone B (top) | `y ∈ {0,1}` → 13 cells |
| "Upper half" for generation (B-21) | `y ≤ 3`, or `y == 4 && x < 6` → 29 cells |
| Rune/Wellspring band, upper half (B-23) | `y ∈ {2,3}` (13 cells). The mirror band is `y ∈ {5,6}` |
| Mirror / pair cell (B-04) | axial `(q,r) → (-q,-r)`. Doubled `(x,y) → (12-x, 8-y)` |

Validity: a cell is on the board if and only if its doubled `(x,y)` is in the set above. Keep a `HashSet<Hex>` built once, and check membership. Do not write a closed-form bounds test.

## Directions

```
Straight (6 edge neighbours), in axial (dq, dr):
  (+1, 0) (+1,-1) (0,-1) (-1, 0) (-1,+1) (0,+1)
Distance:  (|dq| + |dr| + |dq+dr|) / 2
```

There are no "diagonal" (vertex) directions in the rules any more (U-08 was removed in GDD v2.0). Straight lines are still used for the Archer's range and for Push (C-18).

## Movement (GDD U-07, U-09…U-12)

All units use **one** rule: a breadth-first search over edge neighbours.

- **Normal units:** BFS from the unit for up to `Move` steps. A step may enter a cell only if it is on the board, **empty** (no unit, tower or rock) and **Visible** to the moving player (U-12). Every reached cell (except the start) is a legal destination. You may end on the Portal.
- **Rider (U-09):** Same BFS, but it may pass **through** cells that hold a unit or tower (not rock). Those cells are not destinations. Path and destination cells must still be Visible (U-12).
- **Move bonus** (C-14) simply increases `Move` for the BFS.
- **Traps (C-32):** Only the cell where the unit **stops** can trigger a trap. Cells passed through never trigger. So the engine does not need to pick a path; the destination is enough.

Verified reach counts on an **empty, fully visible** board (script-checked):

| Start (doubled x,y) | Axial (q,r) | Move 1 | Move 2 | Move 3 |
|---|---|---|---|---|
| Portal (6,4) | (0,0) | 6 | 18 | 36 |
| Corner (0,0) | (-1,-4) | 2 | 6 | 11 |
| Edge (1,1) | (-1,-3) | 5 | 10 | 17 |

## Visibility (GDD V-01…V-10)

- A player's **Visible** set = all on-board cells within `Sight` distance of any of their units or their tower, plus the Portal (V-06), plus cells of enemy units that are currently revealed (V-08). No line-of-sight blocking.
- **Explored** is a per-player bit set that only grows. Initial value: the player's own half (B-06). B's half = the "upper half" from the board table above (29 cells); A's half = its mirror (29 cells). The Portal belongs to neither half.
- Keep a per-player **last-seen snapshot** for each explored cell (terrain + what was on it). Update it for every cell that is Visible after each command.
- **Control zone (C-02):** cells at distance ≤ `ControlRange` (1) from any own unit or own tower. With every `Sight` ≥ 2, the control zone is always Visible.

## Ranges

- Melee: distance 1.
- **Archer:** Any cell along a straight direction at `k = 1..3`. **Nothing** in between blocks: units, towers and rocks are all ignored (U-03). A line that leaves the board never re-enters it, so stop at the first off-board cell.
- **Mage:** distance 1–2, any direction. The splash hits enemy units and the enemy tower on the target's 6 straight neighbours (U-04).
- **Tower (U-06, U-27):** distance 1–2.
- **Overwatch (U-28):** the unit's normal attack range.
- **Guardian cover (U-11):** When the target is adjacent (distance 1) to a Guardian of its own side and the target is **not a Guardian itself**, it cannot be chosen by an **attack** (this includes overwatch and tower shots). Guardians never receive cover, not even from another adjacent Guardian. A friendly tower adjacent to a Guardian is covered.
- Every attack target must be **Visible** to the attacker's owner (V-07).

## World layout (Unity, `HexLayout`)

```
size   = hex outer radius (world units)
worldX = size * sqrt(3) * (q + r / 2f)
worldZ = -size * 1.5f * r      // row 0 at the top of the screen (+Z) for player A's camera
```

Board extent: `width ≈ 7 * sqrt(3) * size`, `depth ≈ 14 * size`. For player B's view, rotate the camera 180° around Y. Never transform the data.

## Must-have tests (M1)

- There are 59 cells. Both home zones have 13 cells. The upper half has 29 cells. The Portal is at (0,0).
- `Mirror(Mirror(h)) == h`, and `Mirror(h)` is on the board for every `h`.
- The axial↔doubled round-trip works for all 59 cells.
- Both player halves (B-06) have 29 cells, they do not overlap, and together with the Portal they cover all 59 cells.
- Movement BFS on an empty, fully visible board matches the reach-count table above.
- Movement never passes through an occupied cell, except for the Rider; the Rider never passes through rock and never ends on an occupied cell.
- For 1000 seeds, the generated map is exactly symmetric and satisfies B-22 and B-23.
