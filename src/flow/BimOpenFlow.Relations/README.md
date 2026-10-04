# BimOpenFlow.Relations

The pure layers of the table graph described in
`docs/proposals/table-graph-layers.md`: a relational **plan**, **schema**
inference over it, and **compilation** of it to SQL. Nothing here opens a
file or a connection. Rows are produced by `BimOpenFlow.Relations.DuckDb`.

| Folder    | Role | Depends on |
|-----------|------|------------|
| `Plan`    | Immutable operator tree, canonical text, hash, structural equality | the expression AST from `Ara3D.DataFlowEngine.Expressions` |
| `Schema`  | `Infer(plan, catalog)`: typed columns or errors, one rule per operator | `Plan` |
| `Compile` | `Compile(plan)`: one CTE per operator, sources left symbolic | `Plan` |

`Plan` nodes are classes rather than records because their identity is the
canonical text in `PlanText`, which record equality over list fields would
not give. Two plans are equal exactly when their text is equal, and the
hash used by every cache downstream is the SHA-256 of that text.

Sources carry a **source name**, never a path or connection string. The
executor resolves the name through a connection registry.
