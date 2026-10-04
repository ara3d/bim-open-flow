# BimOpenFlow.Nodes.Viz

Chart and table-view nodes that validate and project table data for the web
panes. Rendering stays client-side in `@bimopenflow/viz`; these nodes only shape
what gets rendered, so a graph ending in one of them is a chart or a named table
without any host-side drawing. Exposes `VizNodes.All` for registry composition.
All nodes are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `chart.bar` | table, scale (Table, Optional) | table, legend | labelColumn (Text), valueColumns (Text, comma-separated; empty = every numeric column), title (Text), sort (Enum none/asc/desc, default none) |
| `chart.line` | table | table | xColumn (Text), yColumns (Text, comma-separated; empty = every numeric column), title (Text) |
| `view.table` | table | table | title (Text), columns (Text, comma-separated; empty = all) |
| `view.colormap` | values | legend | valueColumn (Text), colorMap (Enum viridis/category10/redgreen, default viridis), auto (Boolean, default true), min (Number, default 0), max (Number, default 1) |

## Semantics

- The output is the input projected to the named columns, label or x column
  first, rows optionally reordered; the web pane picks the renderer from the
  node kind and reads `title` from the node's parameters.
- `valueColumns`/`yColumns` left empty select every Integer or Number column
  except the label or x column (`VizProjection`).
- `chart.bar` sorting by the first value column is stable; numeric columns
  compare numerically, others ordinally.
- Unknown column names warn and are skipped rather than failing the node.
- `view.colormap` builds the shared colour scale (`BimOpenFlow.Nodes.Support.ColorScale`)
  that `view3d.color` and `chart.bar` also consume through an optional `scale`
  input, so the 3D pane and the chart pane over the same channel show one legend.
  An unknown `valueColumn` warns and the node emits an empty legend table.
- `chart.bar` with a `scale` input appends `r g b` (Number, 0..1) columns after
  the projection, computed from the scale over its `column` (or the projection's
  first value column, with a warning, when that column is absent); it passes the
  scale table through unchanged on `legend` and warns when bars fall outside the
  scale's domain. Any pre-existing `r`, `g`, or `b` columns in the projection are
  dropped first, with a warning. Without a `scale` input, `legend` is
  `ColorScale.EmptyTable()`.

## Errors

These nodes never fail a graph over column names: an unknown name warns through
the evaluation context and is skipped, so a chart over a table whose schema
changed upstream still renders what remains.
