# BimOpenFlow.Nodes.Support

Helpers shared by the node packs, which never reference each other: argument
extraction with the node kind in every error, case-insensitive column lookup,
row selection, a minimal in-memory table, row-ordinal columns for
order-preserving SQL, and content hashing for read caches. It defines no nodes
and has no registry entry point. It targets plain net8.0, so nothing here may
depend on the Windows-only DuckDB project; one-table SQL lives in the data layer
(`BosDuckDbQueries.QueryOver`).

## Contents

| File | Role |
|---|---|
| `NodeArgs.cs` | `TableInput`, `RequiredText`, `RequiredEnum`, `RequiredNumber`, `RequiredDateTime`, `SplitNames`: read inputs and parameters, throwing `ArgumentException` prefixed with the node kind |
| `TableColumns.cs` | `Names`, `ColumnIndex`, `RequireColumn`, `CanonicalName`, `RowCount`, `FreeName`, `WithOrdinal`, `KindName`, `CellText`: column resolution, a collision-free ordinal column so generated SQL never trusts scan order, and wire-kind naming (Boolean/Integer/Number/Text) |
| `TableRows.cs` | `KeepRows`: a copy of a table holding only the given rows, for filters that select without SQL |
| `MemoryTable.cs` | `MemoryTable`, `MemoryColumn`, `MemoryRow`: a minimal immutable in-memory `IDataTable` for summary rows and verdict tables |
| `FileHashes.cs` | `HashFile`: uppercase hex SHA-256 of a file's bytes, the key of the in-memory read caches |
| `ColorMaps.cs` | `Rgb`, `ColorMaps`: gradients (viridis, redgreen) and the category10 palette, moved here unchanged from `Nodes.Geometry` so both Geometry and Viz can reach it without referencing each other |
| `ColorScale.cs` | `ColorScale`: the colour scale behind a `legend` table (auto or manual domain, gradient or categorical), shared by `view3d.color`, `view.colormap`, and `chart.bar` so the three nodes colour cells the same way and never each carry their own domain logic |

## The legend table

`ColorScale.Build` colours one column of a values table and reports what it
did in a `legend` table (`ColorScale.TableName`, columns `column`, `domain`,
`role`, `label`, `value`, `r`, `g`, `b`, `count`; one row per swatch): stop
rows for a gradient, category rows for `category10`, and `below`/`above`/
`missing` rows only when their count is above 0. A manual domain that clamps
values warns once, naming how many of the column's values lie outside it.
`ColorScale.ColorOf` colours a single cell against a built scale;
`ToTable`/`FromTable` round-trip the scale across a node boundary, so a scale
built once can colour more than one consumer's cells identically.

## Conventions

- Column names resolve case-insensitively; `CanonicalName` returns the stored
  spelling so generated SQL quotes the real name.
- Nodes that hand a table to DuckDB call `WithOrdinal` first and `ORDER BY` that
  column, because DuckDB does not guarantee scan order under parallel execution.
- `FileHashes` is uppercase hex, distinct from `Ara3D.DataFlowEngine.Runs.Hashes`
  (lowercase, for persisted run records); these keys never leave process memory.

## Dependencies

`Ara3D.DataFlowEngine.Abstractions` only (`Ara3D.DataTable` arrives through it). Packs that reference
`Support` may not reference any other `Nodes.*` project; the layering tests in
`tests/BimOpenToolkit.Layering.Tests` enforce this.
