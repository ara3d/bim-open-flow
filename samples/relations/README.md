# Relation sample graphs

Graph documents built entirely from the `rel.*` pack, over the CSV files and
`sample.duckdb` in `samples/tables`. With `samples/tables` as a model root
the host registers two sources: `tables` (the folder, for CSV files) and
`sample` (the database file).

| Graph | Shows |
|-------|-------|
| `revenue-by-customer` | two joins, a derived column, an aggregate, and a sort compiled to one statement of eight CTEs |
| `large-orders` | filter, sort, select, limit |
| `orders-per-customer` | a CSV left-joined to a DuckDB table, then counted per customer |
| `monthly-spend-sql` | raw SQL over two CSV relations bound as `t1` and `t2` |
| `schema-error` | a misspelled column, reported on the node before any rows are read |

Nothing in these graphs materializes until a node is inspected. Clicking the
`answer` node in the editor runs one limited query plus a count; clicking an
upstream node runs the statement only up to that node.
