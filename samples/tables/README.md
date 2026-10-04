# Table sandbox sample data

Small hand-authored datasets for the table-only sandbox (customers / orders /
products). The CSVs are the source of truth; the .xlsx, .sqlite, and .duckdb
variants are generated from them by the seeding test in
`tests/BimOpenFlow.TableWorkflows.Tests` (run explicitly) and committed so a
sandbox workflow runs out of the box.

Unlike `data/` (fetched, never committed), everything here is committed.

## sample.bfast

A 448-byte BFAST container for the `bfast.read` and `bfast.buffer` nodes,
written by the SDK writer from `SampleBfast` in
`tests/flow/BimOpenFlow.Nodes.Tables.Tests` (run its Explicit `Regenerate` test
after changing the definition; `CommittedFile_MatchesTheDefinition` keeps the
two equal). BFAST stores no element types, so the table below is the only
record of them:

| Index | Name | Bytes | Read as | Values |
|---|---|---|---|---|
| 0 | `orderIds` | 32 | int32 | 1 to 8 |
| 1 | `quantities` | 32 | int32 | 2, 1, 5, 3, 1, 4, 2, 6 |
| 2 | `unitPrices` | 64 | float64 | 12.5, 30, 4.25, 99.99, 12.5, 4.25, 30, 7.75 |
| 3 | `productCodes` | 25 | uint8 only | `P1`..`P10` as null-terminated ASCII; 25 is not a multiple of any wider element size |
