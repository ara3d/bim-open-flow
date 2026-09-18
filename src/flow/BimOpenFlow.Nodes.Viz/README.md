# BimOpenFlow.Nodes.Viz

Chart and table-view nodes that validate and project table data for the web
panes. Rendering stays client-side in `@bimopenflow/viz`; these nodes only shape
what gets rendered, so a graph ending in one of them is a chart or a named table
without any host-side drawing. Exposes `VizNodes.All` for registry composition.
All nodes are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `chart.bar` | table | table | labelColumn (Text), valueColumns (Text, comma-separated; empty = every numeric column), title (Text), sort (Enum none/asc/desc, default none) |
| `chart.line` | table | table | xColumn (Text), yColumns (Text, comma-separated; empty = every numeric column), title (Text) |
| `view.table` | table | table | title (Text), columns (Text, comma-separated; empty = all) |

## Semantics

- The output is the input projected to the named columns, label or x column
  first, rows optionally reordered; the web pane picks the renderer from the
  node kind and reads `title` from the node's parameters.
- `valueColumns`/`yColumns` left empty select every Integer or Number column
  except the label or x column (`VizProjection`).
- `chart.bar` sorting by the first value column is stable; numeric columns
  compare numerically, others ordinally.
- Unknown column names warn and are skipped rather than failing the node.

## Errors

These nodes never fail a graph over column names: an unknown name warns through
the evaluation context and is skipped, so a chart over a table whose schema
changed upstream still renders what remains.
