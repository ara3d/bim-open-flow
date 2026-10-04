# BimOpenFlow.Nodes.TableOps

The general table vocabulary: filter, derive, aggregate, and sort, then row,
column, reshape, and window transforms over one flowing table, each a typed
facade over one compiled expression or one generated DuckDB clause. Both host
profiles carry this pack. Exposes `TableOpsNodes.All` for registry composition.
All nodes are version 1 and Pure. `SortTerms` is the `col desc, other` parser;
`TableExpressions` bridges table columns to the engine expression grammar.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `table.filter` | table | table | expr (Expression, Boolean over the table's columns) |
| `table.derive` | table | table | name (Text), expr (Expression) |
| `table.aggregate` | table | table | groupBy (Text, comma-separated, may be empty), aggregates (Text, `func(column) as name`, funcs count/sum/min/max/avg) |
| `table.sort` | table | table | by (Text, comma-separated names, optional ` desc` suffix) |
| `table.limit` | table | table | count (Integer), offset (Integer, default 0) |
| `table.distinct` | table | table | columns (Text, comma-separated keys; empty = whole-row distinct) |
| `table.sample` | table | table | mode (Enum rows/fraction, default rows), rows (Integer, default 100), fraction (Number, default 0.1), seed (Integer, default 1) |
| `table.drop` | table | table | columns (Text, comma-separated) |
| `table.rename` | table | table | renames (Text, comma-separated `old=new` pairs) |
| `table.cast` | table | table | column (Text), type (Enum boolean/integer/number/text/date/datetime), onError (Enum error/null, default error), name (Text, empty = in place) |
| `table.splitColumn` | table | table | column (Text), separator (Text, default `-`), names (Text, comma-separated new columns), keep (Boolean, default false) |
| `table.concat` | a, b | table | columns (Enum strict/byName, default strict) |
| `table.pivot` | table | table | groupBy (Text), nameColumn (Text), valueColumn (Text), aggregate (Enum first/sum/count/min/max/avg, default first) |
| `table.unpivot` | table | table | keep (Text), columns (Text), nameColumn (Text, default `name`), valueColumn (Text, default `value`) |
| `table.transpose` | table | table | headerColumn (Text) |
| `table.window` | table | table | function (Enum rowNumber/rank/denseRank/lag/lead/cumSum/movingAvg/percentOfTotal), column (Text), partitionBy (Text), orderBy (Text), offset (Integer, default 1), windowSize (Integer, default 3), name (Text) |
| `table.schema` | table | schema | — |
| `table.profile` | table | profile | — |

## Semantics

- Expressions (`table.filter`, `table.derive`) see the table's scalar columns
  by name; columns with non-scalar .NET types are unavailable. Null cells
  propagate: a null filter result excludes the row, a null derive result
  yields a null cell. `table.aggregate` and `table.sort` run through DuckDB
  with the input as `t`; sums are cast to BIGINT or DOUBLE so the result type
  is predictable.
- The input table is written to an in-memory DuckDB as `t` with a hidden row
  ordinal, so every node preserves input row order; `table.limit` and the window
  functions read that order, never DuckDB scan order.
- `table.distinct` with keys keeps the first row per key with all its columns.
- `table.sample` is deterministic for a given seed: reservoir sampling for a row
  count, Bernoulli for a fraction.
- `table.cast` writes in place when `name` is empty and adds a column otherwise;
  `onError=null` turns unconvertible cells into nulls instead of failing.
- `table.concat` in `strict` mode requires the same column names in the same
  order; `byName` matches by name and fills missing columns with null.
- `table.schema` outputs one row per column (name, type, index).
  `table.profile` runs DuckDB `SUMMARIZE` and projects it to a fixed column set
  (type, counts, distinct count, min, max, mean) with an exact null count.

## Errors

Expression parse or type errors (with character offsets), unknown columns, malformed rename or split specs, a `table.window` name that
already exists or a negative `offset`, and failed casts under `onError=error`
throw `ArgumentException` with the node kind prefixed. `onError=null` warns with
the count of rows that became null.
