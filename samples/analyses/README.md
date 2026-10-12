# Sample analyses

Ready-made table-sandbox graphs. Each file is a canonical graph document
(`.dfg.json` shape) whose file name is the analysis id. File paths inside the
documents use the literal placeholder `{SAMPLES}` for the `samples/tables`
directory; the host rewrites it to the absolute path at seed time.

When the host starts with `--profile tables` and its analysis store is empty,
every document here is copied into the store (see `SampleSeeding` in
`src/BimOpenFlow.Host`). A non-empty store is never touched.

| Id | Shows |
|---|---|
| `customer-revenue` | the SQL showcase: 3 CSVs into a 3-input `sql.query` join, totalled per customer with `table.aggregate`, ranked |
| `category-mix` | revenue per product category with no SQL: `table.join` and `table.derive` price each order, totalled per category, ranked, and drawn with `chart.bar` |
| `sqlite-vs-csv` | `sqlite.query` vs `duck.read` of the same orders; `table.setOp` intersect keeps all 8 of 8 rows, so the sources agree |
| `xlsx-enrichment` | `xlsx.read` products joined onto CSV orders, derived Revenue column, filtered to the 5 order lines over 500 |
| `duckdb-warehouse` | one `duck.query` join over the three tables in sample.duckdb, kept in SQL because the tables share one database file |
| `order-size-split` | orders split by two filters, recombined with `table.setOp` union (large orders first) |

Every graph opens with an `about` note and ends in a node named `answer`.

Every sample validates against `HostComposition.TablePacks()` and evaluates
green over the seeded sample data; `tests/BimOpenFlow.TableWorkflows.Tests`
enforces both.
