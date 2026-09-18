# BimOpenFlow.Nodes.DuckDb

File readers and SQL nodes backed by DuckDB. Every value is a plain table; nothing
here knows about BIM. Exposes `DuckDbNodes.All` for registry composition. All nodes
are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `duck.source` | — | source (Text) | path (FilePath) |
| `duck.query` | source (Text, optional) | table | sql (Text, one read-only SELECT/WITH); path (FilePath, hidden, kept for graphs saved before `duck.source`) |
| `sql.query` | t1, t2–t4 (optional) | table | sql (Text, one read-only SELECT/WITH; inputs available as `t1`..`t4`, `t` = `t1`) |
| `duck.read` | — | table | path (FilePath), format (Enum auto/csv/parquet/json, default auto) |
| `csv.read` | — | table | path (FilePath or glob), delimiter (Text, default `,`), header (Boolean, default true), skipRows (Integer, default 0), quote (Text, default `"`), nullText (Text, default empty), encoding (Enum utf8/utf16/latin1, default utf8), inferTypes (Boolean, default true) |
| `parquet.read` | — | table | path (FilePath or glob) |
| `json.read` | — | table | path (FilePath), layout (Enum auto/records/lines, default auto), flatten (Boolean, default false) |
| `duck.table` | — | table | path (FilePath), table (Text, suggests the file's tables) |
| `duck.tables` | — | tables | path (FilePath) |

## Semantics

- `duck.source` opens a `.duckdb` file read-only and hands its path downstream
  as a Text value. Connected `duck.query` nodes share one connection through
  `DuckSourceCache` (eight databases at most; a changed file stamp or eviction
  reopens). Without a connected source, `duck.query` falls back to its own
  `path` parameter and opens the file per evaluation.
- `sql.query` writes its input tables into an in-memory database as `t1`..`t4`
  and runs the query there, so any DuckDB SQL works over flowing tables.
- The file readers (`csv.read`, `parquet.read`, `json.read`, `duck.read`) cache
  results by the content hash of every matched file plus the parameter values
  (`FileReadCache`): unchanged files never reload, any edit or new glob match does.
- `duck.tables` lists name, columnCount, and rowCount per table.
- Date and timestamp columns come back as ISO-8601 text.

## Errors

Missing files, unresolved globs, SQL that is not a single SELECT/WITH, and bad
parameters throw `ArgumentException` or `FileNotFoundException` with the node kind
prefixed to the message.
