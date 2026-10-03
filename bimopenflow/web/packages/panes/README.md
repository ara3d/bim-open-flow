# @bimopenflow/panes

Pane implementations for the BimOpenFlow editor: table, chart, inspector,
and verdict list. Each pane is an isolated module behind one pane
contract — data in, events out. This package owns the contract.

Depends on `@bimopenflow/contracts` (generated types) and `@bimopenflow/viz`
(table/chart rendering). Plain TS + DOM, no UI framework. Styling is injected once per document
under the `bof-panes-` class/custom-property prefix, the same approach as viz.

## The pane contract (`src/pane.ts`)

```ts
interface PaneContext {
  requestTable(nodeId: string, port: string, skip?: number, take?: number): Promise<TableSlice>;
  resolveAsset(url: string): string;
  requestSuggestions?(nodeId: string, param: string): Promise<SuggestionList>;
  requestEntityProperties?(modelUrl: string, localId: number): Promise<EntityProperties>;
}

interface Pane {
  mount(el: HTMLElement, ctx: PaneContext): void;
  update(input: PaneInput): void;
  onEvent(handler: (e: PaneEvent) => void): void;
  destroy(): void;
}
```

Lifecycle: mount once, update any number of times, destroy once (idempotent).
`update` before mount throws; `onEvent` supports multiple handlers. Panes
ignore input kinds they do not handle. All inputs and events are
JSON-serializable.

```ts
type PaneInput =
  | { kind: "table"; data: TableSlice }
  | { kind: "nodeState"; state: NodeState }
  | { kind: "selection"; ids: string[] }
  | { kind: "inspect"; node: NodeDescriptor; values: Record<string, string>; state?: NodeState }
  | { kind: "model"; url: string; format?: "bos" | "glb" }
  | { kind: "instances"; data: TableSlice };

type PaneEvent =
  | { kind: "selection"; event: SelectionEvent }
  | { kind: "action"; action: string; payload?: Record<string, string> };
```

## Panes

Each pane is created by a factory that takes its options and returns a `Pane`.

### TablePane — `createTablePane(options?)`

Wraps the viz `DataTableView` (options pass through: `maxRows`, `sortable`).

- Accepts: `table` (render/update), `selection` (highlight matching rows).
- Emits: `selection` with `source: "table"` on row click.
- Id column heuristic: `globalId` if present, else `entityId`, else the
  first column. The id is the rendered cell text, so Integer ids become
  their plain decimal string. Highlights survive the viz table's own
  header-click re-sort (reapplied via MutationObserver).

### ChartPane — `createChartPane({ chart: "bar" | "line", ...options })`

Wraps the viz `BarChart` or `LineChart`; the remaining options are the chosen
chart's viz options, passed through unchanged (`categoryColumn`,
`valueColumn`, `xColumn`, `seriesColumns`, `width`, `height`).

- Accepts: `table`.
- Emits: nothing.

### InspectorPane — `createInspectorPane()`

Definition list of a node's params, ports, and status. Plain DOM.

- Accepts: `inspect` (node + param values + optional state), `nodeState`
  (replaces the status section of the currently inspected node). Renders
  nothing until the first `inspect`.
- Emits: nothing.
- Param rows show the provided value, falling back to the param's default.

### VerdictPane — `createVerdictPane()`

Renders a verdict table (compliance convention: `verdict`, `checkId`,
`checkTitle`, `citation` columns — see
`src/BimOpenFlow.Nodes.Compliance/README.md`) as one block per check, grouped
by `checkId` in first-appearance order, with count chips per verdict and
border coloring by the group's most severe verdict
(Fail > NeedsReview > InfoNotAvailable > Pass).

- Accepts: `table` (a verdict table; throws on missing convention columns or
  unknown verdict text), `selection` (highlights checks containing a
  selected id).
- Emits: `selection` with `source: "verdict"` on check click — the distinct
  ids of that check's rows, using the same id column heuristic as TablePane.

### The 3D pane

`createViewPane3D` and its registration `view3dPane` live in
`@bimopenflow/pane-3d` (`packages/pane-3d/README.md`), so this package does
not depend on the BIM Open viewer. It uses this package's contract,
`instanceTable`, `boxTable`, the legends (`legend.ts`, `instanceLegend.ts`,
`scaleLegend.ts`), and `entityProperties`; its tests use the conformance suite
and helpers exported as `@bimopenflow/panes/testing`.

## Development

No `npm install` has been run for this package yet (the lockfile belongs to
another track this wave). Type and test resolution use the sibling
workspaces' existing installs:

- `@bimopenflow/contracts` and `@bimopenflow/viz` resolve through
  `bimopenflow/web/node_modules` (already installed).

```sh
npx tsc -p . --noEmit   # typecheck
npx vitest run          # tests (jsdom)
```
