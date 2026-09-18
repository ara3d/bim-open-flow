# BimOpenFlow.Nodes.Spatial

The `spatial.*` pack: the predicate and measure vocabulary of a GIS database
(intersects, within, nearest, contains, footprint, polygon measures) over
ordinary table columns. BIM-free and DuckDB-free; geometry math comes from
`Ara3D.Geometry`. Exposes `SpatialNodes.All` for registry composition. All
nodes are version 1 and Pure. Design and scope:
[docs/proposals/spatial-node-set.md](../../../docs/proposals/spatial-node-set.md).

## Shapes as columns

| Shape | Columns |
|---|---|
| Box | `MinX MinY MinZ MaxX MaxY MaxZ` (any case, so the Geometry pack's `minX` works too) |
| Point | the columns named by the `x`, `y`, `z` params, default `CenterX CenterY CenterZ`; used when the box columns are absent |
| Polygon | one Text column of WKT `POLYGON((x y, x y, ...))`, named by the `polygon` param, default `Footprint` |

A point is a degenerate box, so every box node accepts a point table on either
side. Rows with a missing coordinate never match. A box with Min above Max is
an error naming the row.

## Join output

Every join node emits a **pairs** table: `A` (a's key), `B` (b's key), each
typed like its source column, then the node's measure. Use `table.join` to
bring the rest of either side's columns back. A table joined to itself reports
each unordered pair in both directions; `excludeSelf` (default true) drops the
pairs whose two keys are equal.

## Nodes

| Kind | Inputs | Params | Output |
|---|---|---|---|
| `spatial.intersects` | a, b | aKey, bKey, x, y, z, excludeSelf | pairs + `OverlapVolume` |
| `spatial.within` | a, b | distance, measure (box \| center), aKey, bKey, x, y, z, excludeSelf | pairs + `Distance` |
| `spatial.nearest` | a, b | k (default 1), measure, aKey, bKey, x, y, z, excludeSelf | pairs + `Distance`, `Rank` |
| `spatial.contains` | a, boxes | aKey, bKey, x, y, z, smallest (default true), ignoreZ, excludeSelf | pairs + `ContainerVolume` |

| `spatial.footprint` | boxes | as (default `Footprint`) | input + WKT rectangle |
| `spatial.polygon` | table | polygon | input + `Area`, `Perimeter`, `CentroidX`, `CentroidY`, `Vertices`, `IsConvex` |
| `spatial.polygonContains` | a, polygons | aKey, bKey, x, y, z, polygon, smallest | pairs + `Area` |
| `spatial.polygonIntersects` | a, b | aKey, bKey, polygon, excludeSelf | pairs |

`measure = box` is the distance between box surfaces, zero when they intersect;
`center` is the distance between box centers. `spatial.nearest` searches the
index in a doubling radius, so a row never scans all of `b` unless the answer
needs it; `spatial.contains` with `smallest` keeps the container of least
volume (least footprint area under `ignoreZ`).

Polygons are plan (XY) shapes. `Polygon` keeps vertices in double precision and
hands Ara3D.Geometry's `PolygonOps` a copy translated to the polygon's own
bounds minimum, so single-precision rounding scales with the polygon's extent
rather than with site coordinates. Area, perimeter, convexity, point
containment, and edge crossing come from the SDK; the area-weighted centroid is
computed here because the SDK's `Centroid` is the vertex average. Only outer
rings are used; holes raise one warning per input.

## Semantics and limits

- Box predicates are axis-aligned-box semantics. Results are candidates, not
  mesh clashes or clearances.
- Intervals are closed: touching boxes intersect with zero overlap volume.
- Candidates come from an `AabbTree` over `b` (single precision, padded
  outward); every predicate is then decided exactly in double precision, so
  joins cost O((n + m) log m) plus the output size.

## Files

- `SpatialColumns.cs` — the column vocabulary.
- `Box.cs` — the exact box type and its predicates.
- `Shapes.cs` — reading a table side as boxes or points; shared param specs.
- `BoxIndex.cs` — the candidate index.
- `Pairs.cs` — the pairs-table builder.
- `Measures.cs` — the box and center distance measures.
- `Wkt.cs` — POLYGON and POINT text in and out.
- `Polygon.cs` — the double-precision polygon over the SDK's polygon ops.
- One file per node.
