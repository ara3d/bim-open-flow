# BimOpenFlow.Nodes.DuckDb

File readers and SQL nodes backed by DuckDB. Every value is a plain table; nothing
here knows about BIM. Exposes `DuckDbNodes.All` for registry composition. All nodes
are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `duck.source` | — | source (Database) | path (FilePath) |
| `duck.query` | — | table | path (FilePath, bindable: Database), sql (Text, one read-only SELECT/WITH) |
| `sql.query` | t1, t2–t4 (optional) | table | sql (Text, one read-only SELECT/WITH; inputs available as `t1`..`t4`, `t` = `t1`) |
| `duck.read` | — | table | path (FilePath), format (Enum auto/csv/parquet/json, default auto) |
| `csv.read` | — | table | path (FilePath or glob), delimiter (Text, default `,`), header (Boolean, default true), skipRows (Integer, default 0), quote (Text, default `"`), nullText (Text, default empty), encoding (Enum utf8/utf16/latin1, default utf8), inferTypes (Boolean, default true) |
| `parquet.read` | — | table | path (FilePath or glob) |
| `json.read` | — | table | path (FilePath), layout (Enum auto/records/lines, default auto), flatten (Boolean, default false) |
| `duck.table` | — | table | path (FilePath, bindable: Database), table (Text, suggests the file's tables) |
| `duck.tables` | — | tables | path (FilePath, bindable: Database) |

## Semantics

- `duck.source` opens a `.duckdb` file read-only and hands it downstream as a
  Database value: the path plus the SHA-256 of the file's content, which is the
  value's identity (a changed file is a new value). Connected `duck.query` nodes
  share one connection through `DuckSourceCache` (eight databases at most; a
  changed file stamp or eviction reopens). `duck.query`, `duck.table` and
  `duck.tables` take the path as a bindable parameter: a Database value wired
  into `path` replaces the typed string; unwired, they open the typed path per
  evaluation. A host pages a Database value as its table list, the rows
  `duck.tables` returns (`DuckTableList`).
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
