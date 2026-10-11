# BimOpenFlow.Nodes.Value

Constant nodes. Each holds one value in its parameter and emits it, so a value that
several nodes need is set once and wired into each node's bindable parameter socket,
the same as a value typed inline. Exposes `ValueNodes.All` for registry composition.
All nodes are version 1, Pure, and have no inputs.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `value.text` | none | value (Text) | value (Text) |
| `value.number` | none | value (Number) | value (Number, default 0) |
| `value.integer` | none | value (Integer) | value (Integer, default 0) |
| `value.boolean` | none | value (Boolean) | value (Boolean, default false) |
| `value.path` | none | path (Text) | path (FilePath, offers files under the model roots) |

## Semantics

- The output is the parameter parsed with the invariant culture; a malformed number,
  integer, or boolean throws when the node runs.
- `value.path` emits the path as written. It does not check that the file exists; the
  editor's path control shows a missing-file warning.
