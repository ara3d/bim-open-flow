# BimOpenFlow.Relations.DuckDb

The Execute layer of the table graph, and the DuckDB-backed catalog. This is
the only project in the relational stack that opens files or connections.

| File | Role |
|------|------|
| `ConnectionRegistry.cs` | Source name to location: a DuckDB file or a folder of CSV files; `SourceRegistries.FromRoots` snapshots a set of roots |
| `RootScanRegistry.cs` | The same naming over roots, rescanned on every lookup, so a database written later resolves without a restart |
| `PreparingRegistry.cs` | Wraps a registry with the sources still being prepared; an unresolved one fails with "not ready yet" and why, not "unknown" |
| `DuckDbSession.cs` | An in-memory connection with every source a query uses attached under its name |
| `DuckDbCatalog.cs` | `ICatalog` answered by `information_schema` and `DESCRIBE` |
| `DuckDbExecutor.cs` | Runs a compiled query, optionally limited, and checks the result against the inferred schema |
| `DuckDbTypes.cs` | DuckDB and CLR type names to `ColumnType` |
| `InlineTableStore.cs` | In-process tables an `InlineTable` plan node reads, keyed by content hash, bounded by entry count |
| `ResultCache.cs` | Materialized results keyed by plan hash and limit, bounded by entry count |

A compiled query refers to sources as `"name"."reference"`. The session makes
those names real: a DuckDB file is attached read-only as catalog `name`, and a
folder becomes schema `name` holding one view per CSV file the query reads.
Inline tables are different: their rows are already in memory, so the session
writes each one into schema `_inline` before the statement runs.
