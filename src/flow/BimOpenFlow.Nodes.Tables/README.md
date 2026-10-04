# BimOpenFlow.Nodes.Tables

XLSX, SQLite, and BFAST readers and catalogs, the join and set-operation combinators,
column projection, and small table generators (inline JSON, numeric range,
calendar). BIM-free and DuckDB-free. Exposes `TableNodes.All` for registry
composition. All nodes are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `xlsx.read` | — | table | path (FilePath), sheet (Text, default first sheet), headerRow (Integer, default 1), range (Text, A1-style like `B3:F100`, default used range) |
| `xlsx.sheets` | — | sheets | path (FilePath) |
| `sqlite.query` | — | table | path (FilePath), sql (Text, one read-only SELECT/WITH) |
| `sqlite.table` | — | table | path (FilePath), table (Text) |
| `sqlite.tables` | — | tables | path (FilePath) |
| `bfast.read` | — | buffers | path (FilePath) |
| `bfast.buffer` | — | table | path (FilePath), name (Text, a buffer name from `bfast.read`), type (Enum uint8/int16/int32/int64/float32/float64, default float32) |
| `table.join` | a, b | table | aKey (Text), bKey (Text, default aKey), mode (Enum left/inner/full/semi/anti, default left) |
| `table.setOp` | a, b | table | op (Enum union/intersect/subtract, default intersect), key (Text) |
| `table.project` | table | table | columns (Text, comma-separated, kept in that order) |
| `table.inline` | — | table | rows (Json array of objects, e.g. `[{"type":"Wall","rate":120.5}]`) |
| `table.range` | — | table | name (Text, default `value`), start (Number, default 0), stop (Number), step (Number, default 1) |
| `table.calendar` | — | table | name (Text, default `date`), start (DateTime), end (DateTime), step (Enum day/week/month/quarter/year, default day) |

## Semantics

- `table.join` keeps a's columns and row order and attaches b's columns by key.
  `left` keeps every a row (b side null when unmatched); `inner` keeps matches;
  `full` also appends b rows no a row matched; `semi` and `anti` keep only a rows
  with or without a match and attach no b columns. Duplicate keys in b warn and
  the first row wins.
- `table.setOp` compares key text (trimmed, invariant); null keys never match.
  `union` appends b rows whose key a lacks, so b must share a's column set.
- `table.project` warns on unknown names and skips them.
- `table.inline` infers one type per column (bool/integer/number/text); integers
  and numbers in one column widen to number, any other mix is an error.
- `xlsx.read` returns dates as ISO-8601 text; a column whose non-null cells share
  one type keeps it, otherwise it becomes text. `xlsx.sheets` lists name, index
  (1-based), rowCount, and columnCount of the used range.
- SQLite columns are dynamically typed per row: one non-null CLR type wins,
  long plus double widens to double, anything else becomes text.
- `bfast.read` lists `name`, `byteLength`, and `index` (0-based file order) from
  the container's directory without reading buffer bytes. BFAST records no
  element types, so `bfast.buffer` reinterprets one buffer under the `type` you
  name and returns a single `value` column: integer types widen to long, float
  types to double. Both are cached by file content hash (plus name and type).

## Errors

Missing files, unknown columns, malformed ranges, SQL that is not a single
SELECT/WITH, an unknown BFAST buffer name (the message lists the names present),
and a buffer whose byte length is not a multiple of the element size throw
`ArgumentException` with the node kind prefixed to the message.
