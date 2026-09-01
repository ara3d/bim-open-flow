# Table sandbox sample data

Small hand-authored datasets for the table-only sandbox (customers / orders /
products). The CSVs are the source of truth; the .xlsx, .sqlite, and .duckdb
variants are generated from them by the seeding test in
`tests/BimOpenFlow.TableWorkflows.Tests` (run explicitly) and committed so a
sandbox workflow runs out of the box.

Unlike `data/` (fetched, never committed), everything here is committed.
