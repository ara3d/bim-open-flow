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
| `customer-revenue` | 3 CSVs into a 3-input `sql.query` join, aggregated per customer, sorted |
| `category-mix` | orders x products join, revenue aggregated per product category |
| `sqlite-vs-csv` | `sqlite.query` vs `duck.read` of the same orders, `table.setOp` intersect proves parity |
| `xlsx-enrichment` | `xlsx.read` products joined onto CSV orders, derived Revenue column, filtered |
| `duckdb-warehouse` | one `duck.query` join over the three tables in sample.duckdb, then `table.project` |
| `order-size-split` | orders split by two filters, recombined with `table.setOp` union, projected |

Every sample validates against `HostComposition.TablePacks()` and evaluates
green over the seeded sample data; `tests/BimOpenFlow.TableWorkflows.Tests`
enforces both.
