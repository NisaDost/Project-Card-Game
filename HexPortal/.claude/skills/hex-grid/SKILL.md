---
name: hex-grid
description: Exact hex math for HexPortal — board layout (GDD v3.3: radius-7 hexagon, 169 cells, 4 map templates with rock and Void cells), axial coordinates, directions, distance, movement BFS (incl. Rider pass-through), ranges, visibility/fog and control zone, 180° symmetry, home zones, player halves and world positions. Load before writing or reviewing any coordinate, movement, range, visibility or map-generation code.
---
# HexPortal hex grid reference

These values were verified by tests (`BoardTests`, `UnitTests`, `MapGenTests`, `MapEventTests`, `VoidTests`). Do not re-derive them. Use them exactly as written here.

## Board shape (GDD v3.3, B-01…B-06)

The hexes are **pointy-top**. The board is a **hexagon of radius 7** around the Portal: **169 cells** (`Board.Cells`, static, never changes). A map template (B-25) may mark some cells **Void**; see "Templates and Void".

Use **axial** coordinates `(q, r)` with the origin at the Portal. The cube coordinate is `s = -q - r`. There is no other storage coordinate system.

```
on board  <=>  max(|q|, |r|, |q + r|) <= 7          (Catalog.BoardRadius)
row r has q from max(-7, -7 - r) to min(7, 7 - r)
```

`Board.Cells` is in a fixed order: by `r` (top row first, `r = -7`), then by `q`. `Board.IndexOf(h)` is the position in that list (-1 off the board); per-cell arrays (`GameMap`, `FogMemory`, `PlayerProgress`) use it.

There are 15 rows. GDD rows are 1-based, row 1 at the top (player B's edge): `GddRow = r + 8`, `GddCol = q - max(-7, -7 - r) + 1` (1 = leftmost).

| Thing | Value |
|---|---|
| Row lengths (GDD rows 1..15) | 8, 9, 10, 11, 12, 13, 14, **15**, 14, 13, 12, 11, 10, 9, 8 |
| Portal (B-02) | `(0,0)` = GDD row 8, col 8 |
| Home zone B (top, B-03) | `r <= -6` (GDD rows 1–2) → 8 + 9 = **17** cells |
| Home zone A (bottom, B-03) | `r >= 6` (GDD rows 14–15) → **17** cells |
| Shortest distance home zone → Portal | **6** (e.g. `(0,-6)`, `(6,-6)`) |
| "Upper half" = B's half (B-21, B-06) | `r < 0`, or `r == 0 && q < 0` → **84** cells (GDD rows 1–7 + the 7 cells left of the Portal) |
| A's half (B-06) | the mirror of B's half: `r > 0`, or `r == 0 && q > 0` → **84** cells |
| Rune/Wellspring band, upper half (B-25) | `r ∈ {-5,-4,-3}` (GDD rows 3–5) → **33** cells. Mirror band: `r ∈ {3,4,5}` (GDD rows 11–13) |
| Mirror / pair cell (B-04) | `(q,r) → (-q,-r)`; GDD row n ↔ row 16 - n |
| Corners | `(0,-7) (7,-7) (7,0) (0,7) (-7,7) (-7,0)`; each has **3** on-board neighbours |
| TestBoard default towers | A `(-3,6)`, B `(3,-6)` (inner home row; the back rows are `r = ±7`) |

Validity: `Board.IsOnBoard(h)` (a dictionary built once). `Board.AddWithin(set, center, radius)` adds the on-board ball around a cell in `Board.Cells` order (use it instead of scanning all 169 cells). `Board.AddWithin(set, center, radius, map)` does the same but skips Void cells (sight, Control Zone).

## Templates and Void (B-25)

- `Catalog.MapTemplates` (4): **0 Plain**; **1 RockyRing** (rocks at distance 2 from the Portal: upper `(1,-2) (2,-1) (-1,-1) (-2,0)` + mirrors; gaps `(0,-2) (2,-2)` + mirrors); **2 IndentedEdges** (Void = every cell within distance 2 of the side corners `(-7,0)` and `(7,0)`, 9 each, 18 total; the other 4 corners are in the home zones, which stay complete); **3 TwoCorridors** (rock wall on `r = -2` and its mirror `r = 2`, gaps at `q ∈ {-2,-1}` and `{3,4}` on `r = -2`).
- Template = `seed % 4` (`MapGenerator.TemplateFor`), stored as `GameMap.TemplateId`. Template cells are given for the upper half and mirrored; they are never in a home zone or in the special band.
- Template rock = `Tile(Biome.None, Marker.Rock)`, permanent (B-14). Void = `Tile(Biome.None, Marker.Void)`. E-10 rock keeps its biome.
- N = playable non-rock upper cells: Plain 84, RockyRing 80, IndentedEdges 75, TwoCorridors 75. Biome minimum `floor(0.28 × N)` = 23 / 22 / 21 / 21 (`Catalog.MinCellsPerBiome(N)`); 6–8 seeds per biome.
- `GameMap.IsPlayable(h)` = on board and not Void; `GameMap.IsPassable(h)` = also not rock; `GameMap.PlayableCells` / `PlayerView.PlayableCells` = non-Void cells in `Board.Cells` order (client drawing and camera framing).
- Void: impassable (also for the Rider), never Visible or Explored, never in the Control Zone, so never a deploy/trap/teleport cell; push stops before it; never an event target (E-10/E-12 need `Marker.None`, E-11 cells need a biome). `PlayerView` shows Void cells as Hidden with `TerrainKnown = true` and `Tile = Void` (the shape is public).

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

- **Normal units:** BFS from the unit for up to `Move` steps. A step may enter a cell only if it is on the board, **empty** (no unit, tower, rock or Void) and **Visible** to the moving player (U-12). Every reached cell (except the start) is a legal destination. You may end on the Portal.
- **Rider (U-09):** Same BFS, but it may pass **through** cells that hold a unit or tower (not rock, not Void). Those cells are not destinations. Path and destination cells must still be Visible (U-12).
- **Move bonus** (C-14) simply increases `Move` for the BFS.
- **Traps (C-32):** Only the cell where the unit **stops** can trigger a trap. Cells passed through never trigger. So the engine does not need to pick a path; the destination is enough.

Verified reach counts on an **empty, fully visible** board (= the on-board distance ball minus the start; `BoardTests.Hex_ReachCounts`):

| Start (q,r) | Where | Move 1 | Move 2 | Move 3 |
|---|---|---|---|---|
| (0,0) | Portal | 6 | 18 | 36 |
| (0,-7) | corner | 3 | 8 | 15 |
| (3,-7) | middle of the top edge | 4 | 11 | 21 |

Any edge cell at distance ≥ 3 from both corners of its edge (e.g. `(7,-3)`) has the same counts as `(3,-7)`.

## Visibility (GDD V-01…V-10)

- A player's **Visible** set = all playable (non-Void) cells within `Sight` distance of any of their units or their tower, plus the Portal (V-06), plus cells of enemy units that are currently revealed (V-08). No line-of-sight blocking.
- **Explored** is a per-player bit set that only grows. Initial value: the player's own half (B-06, 84 cells) minus Void. The Portal belongs to neither half.
- Keep a per-player **last-seen snapshot** for each explored cell (terrain + what was on it). Update it for every cell that is Visible after each event (`Visibility.UpdateMemory`, index-based hot path).
- **Control zone (C-02):** playable cells at distance ≤ `ControlRange` (1) from any own unit or own tower. With every `Sight` ≥ 2, the control zone is always Visible.

## Ranges

- Melee: distance 1.
- **Archer:** Any cell along a straight direction at `k = 1..3`. **Nothing** in between blocks: units, towers, rocks and Void are all ignored (U-03). A line that leaves the board never re-enters it (the 169-cell hexagon is convex), so stop at the first off-board cell.
- **Mage:** distance 1–2, any direction. The splash hits enemy units and the enemy tower on the target's 6 straight neighbours (U-04).
- **Tower (U-06, U-27):** distance 1–2.
- **Overwatch (U-28):** the unit's normal attack range.
- **Guardian cover (U-11):** When the target is adjacent (distance 1) to a Guardian of its own side and the target is **not a Guardian itself**, it cannot be chosen by an **attack** (this includes overwatch and tower shots). Guardians never receive cover, not even from another adjacent Guardian. A friendly tower adjacent to a Guardian is covered. Only Guardians that are **Visible to the attacking side** count (V-11); for tower shots and overwatch the attacking side is the shooter's owner.
- Every attack target must be **Visible** to the attacker's owner (V-07).

## Map generation and events (B-20…B-25, E-*)

- Generation lays the template's rock/Void cells, grows biomes on the N playable non-rock upper cells (B-21, B-22, B-25), places 2 runes + 1 wellspring in the band, then mirrors. `MapValidator` also checks the template cells, complete and connected home zones and the Portal reachable from every home cell (BFS over passable cells, no units).
- Seeds 1–1000 (`MapGenTests` stats): first-attempt validity Plain **99.2%**, RockyRing **98.8%**, IndentedEdges **98.8%**, TwoCorridors **96.0%**; max **2** attempts; 1000/1000 distinct maps. Biome clusters (mean / mean largest / max): Plain **8.02 / 21.6 / 30**, RockyRing **7.21 / 19.3 / 29**, IndentedEdges **7.32 / 19.7 / 27**, TwoCorridors **6.44 / 18.3 / 27**. Test limits keep the v3.0 limits (mean 7.5, mean largest 20 at 3.5 cells per seed) per seed: both × N / 73.5.
- E-10/E-12 targets: mirror pairs outside both home zones with no marker (so never rock, Void or special). E-11: center at distance ≥ 3 from the Portal with a biome; the cluster is the center plus its neighbours that have a biome (never the Portal, Void or template rock); it never overlaps its mirror (that needs distance ≤ 1). Minimum terrain-eligible targets over seeds 1–1000 (mirror pairs): **E-10 55, E-11 66, E-12 55** (per template: Plain 64/75/64, RockyRing 60/75/60, IndentedEdges 55/66/55, TwoCorridors 55/69/55).

## World layout (Unity, `HexLayout`)

```
size   = hex outer radius (world units)
worldX = size * sqrt(3) * (q + r / 2f)
worldZ = -size * 1.5f * r      // r = -7 (B's edge) at the top of the screen (+Z) for player A's camera
```

Full-hexagon extent (centre to centre): `width = 14 * sqrt(3) * size ≈ 24.2 * size` (row `r = 0`), `depth = 21 * size`. Draw and frame only `PlayableCells` (Void cells are not drawn). For player B's view, rotate the camera 180° around Y. Never transform the data.

## Must-have tests

- There are 169 cells, rows 8..15..8. Both home zones have 17 cells at distance ≥ 6 from the Portal. The upper half has 84 cells. The Portal is at (0,0).
- `Mirror(Mirror(h)) == h`, and `Mirror(h)` is on the board for every `h`.
- `Board.Cells` equals the axial hexagon `max(|q|,|r|,|q+r|) ≤ 7`, ordered by r then q.
- Both player halves (B-06) have 84 cells, they do not overlap, and together with the Portal they cover all 169 cells.
- Movement BFS on an empty, fully visible board matches the reach-count table above.
- Movement never passes through an occupied cell, except for the Rider; the Rider never passes through rock or Void and never ends on an occupied cell.
- For 1000 seeds, the generated map is exactly symmetric, follows its template (`seed % 4`) and satisfies B-22, B-23 and B-25 (home zones complete and connected, the Portal reachable from every home cell); every map-event type has a terrain-eligible target on every template.
- Void is never passable, Visible, Explored, in the Control Zone or an event target (`VoidTests`).
