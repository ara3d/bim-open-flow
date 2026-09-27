# BimOpenFlow.GraphText

Prints a graph document and its evaluation snapshot as deterministic text, so a
test or an agent can check a built graph without a screenshot (TKT-57).

```
dfg 0.1.0;
// sum   graph 3f0c9a1b2d4e
a = test.const@1(kind: "Integer", value: "2");
  // Ok  Integer 2
sum = test.add@1(a: a.out, b: b.out);
  // Ok  Integer 5
```

One binding per node, `id = kind@version(port: node.port, param: "value");`, in
the engine's evaluation order (topological, ties by node id). Under each
binding a comment block gives the node's status and, for anything but `Ok`,
the cause the engine reports; then one digest per output: scalars as their
value, tables as shape, columns, first rows, and a short content hash,
relations through an `IRelationReader` (count, then a bounded page) or as their
plan alone.

## Entry point

`GraphText.Print(GraphDocument doc, EvalSnapshot? snapshot, INodeRegistry registry, GraphTextOptions? options)`

- `doc` is printed and hashed as written, placeholders such as `{SAMPLES}`
  intact; the snapshot may come from a copy whose paths were rewritten for
  evaluation, since results are matched by node id.
- `GraphTextOptions.Golden` (the default) is byte-stable across runs and
  machines: four significant digits, no engine hashes or execution counts, and
  every directory in `PathAliases` printed as its alias. `Mode = Debug` adds
  execution counts and full output hashes and prints numbers in full.

## Files

| File | Role |
|---|---|
| `GraphText.cs` | Entry point and header line |
| `Bindings.cs` | Node order and binding lines |
| `ResultText.cs` | Status, cause, warnings, and output layout under a binding |
| `ValueText.cs` | Digest per value kind; relations through the reader |
| `TableText.cs` | Table shape, columns, sample rows, content hash |
| `Literals.cs` | Quoting, number rounding, cell spelling |
| `GraphTextOptions.cs` | Mode, aliases, caps, relation reader |

## Dependencies

The engine (`Ara3D.DataFlowEngine`, `Ara3D.NodeGraph`) and
`BimOpenFlow.Nodes.Support` for cell helpers. Not the host: the host and the
MCP server call this library, never the other way round.
