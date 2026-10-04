# BimOpenFlow.Nodes.Relations

The `rel.*` node pack: nodes whose wires carry a **relation** (a logical plan
plus its schema) instead of rows. A chain of these nodes compiles to one SQL
statement and runs only when a node is inspected, a count is asked for, or
`rel.materialize` turns the relation into an ordinary table for the row-based
packs.

| Node | Builds |
|------|--------|
| `rel.csv`, `rel.table` | a source, by source name and file or table |
| `rel.fromTable` | a relation over an input Table, so any row-based node can feed the pack |
| `rel.select`, `rel.filter`, `rel.derive`, `rel.sort`, `rel.limit` | one operator over one input |
| `rel.join`, `rel.aggregate` | join by keys; group and aggregate |
| `rel.sql` | user SQL over up to three inputs named t1..t3 |
| `rel.materialize` | a relation into a materialized Table, optionally limited |

Every node validates its plan's schema when it evaluates, so a missing column
or a type error marks the node red before any rows are read. `RelationRuntime`
holds the catalog, connection registry, schema cache, and result cache the
pack shares; the host builds one from its configured roots.
