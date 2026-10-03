# BIM Open Flow

![BIM Open Flow](docs/brand/lockup.svg)

Graphs that turn questions about building data into small, inspectable, reproducible computations, built the same way by people in a web editor and by AI agents over MCP.

**Status on 2026-10-03: the code is not here yet.** It lives in [`ara3d/bim-open-toolkit`](https://github.com/ara3d/bim-open-toolkit): the .NET side in `src/flow` (tests in `tests/flow`), the web editor in `bimopenflow/web`, the MCP server in `src/mcp/BimOpenMcp.Flow`, and the Ask box's agent loop in `src/studio/BimOpenFlow.Ask`. The generic part moves here, with its git history, in phase 5 of the toolkit's [repository split plan](https://github.com/ara3d/bim-open-toolkit/blob/main/docs/plans/repository-split.md), after phase 3 has made the host and the editor take node packs and panes from whoever composes them. Until then this README describes what will arrive, and the toolkit is where to build, test, and file issues.

## What a graph is

A graph is a JSON document of nodes and the wires between them. Each node is a small function with typed input and output ports and parameters edited on the node itself: `table.filter` takes a table and an expression and returns a table; `chart.bar` takes a table and draws it. A door schedule, rooms per storey, a check that every door leaf is at least 850 mm wide, or a 3D view coloured by category are each a graph of a handful of nodes.

```
  load ──┬─► table.filter ──► table.aggregate ──┬─► chart.bar        (chart)
         │                                      └─► sink.exportCsv  (effect, waits for Run)
         └─► view3d.instances ──► view3d.color ────► 3D pane         (3D view)
```

Editing a graph means calling one of four operations: `addNode`, `connect`, `setParam`, `removeNode`. They back the HTTP API, the MCP tools, and every gesture in the editor, and a whole document written at once is validated as if it had been built from them. A graph an agent builds is one a person could have built, and there is no second scripting API to drift away from the editor.

## Value kinds

Six kinds of value travel on wires: Boolean, Integer, Number, Text, Table, and Relation. Almost everything useful is an immutable **Table**. A **Relation** is a query plan and its schema rather than rows; rows exist only once something executes the plan. (The toolkit's `PROJECT.md` and `ARCHITECTURE.md` still say five kinds; Relation was added to the engine later for the `rel.*` nodes.) A new kind of input is a parameter kind, never a new wire type, so one vocabulary serves loading, SQL, table operations, charts, 3D, and rule checks.

## Pure nodes, effects, and Run

Nodes are either **pure** or **effects**. A pure node only computes; its results are memoized and re-evaluated whenever an input changes. An effect node writes something: a CSV, Excel, or Parquet file, a report, or property sets into an IFC file. Every effect node lives in one pack (`BimOpenFlow.Nodes.Effects`), so purity is enforced by project reference.

Evaluating a graph for display never runs an effect; it reports the node as `EffectPending`. Effects run only inside an explicit **Run**. Exploring a model, by a person or an agent, can therefore never touch a file.

Evaluation reports a state for every node: `Ok`, `Unready` (an input is missing), `EffectPending`, `Unavailable`, or `Error`. A half-wired graph is a normal thing to look at and repair.

## Runs are the evidence

A run record pins the hash of the graph and the content hash of every input alongside the outputs, and can be replayed. When an agent reports a number, it comes from a run or a tool result, with a derivation anyone can rerun. The publishing projects turn a run into a self-contained HTML report, a dashboard, or an evidence package: a zip whose `manifest.json` lists a SHA-256 per member.

## What will arrive

| Part | From the toolkit | Contents |
|---|---|---|
| Contracts | `src/flow/BimOpenFlow.Contracts` | Wire types shared with the web client |
| Node packs | `src/flow/BimOpenFlow.Nodes.*` | One project per vocabulary: tables, table operations, cleaning, dates, DuckDB, relations, spatial, geometry, compliance checks, charts and views, effects. `Nodes.Support` holds shared helpers. |
| Host | `BimOpenFlow.Host.Catalog`, `.Host.Store`, `.Host.Api`, `BimOpenFlow.Host` | Model discovery, graphs and runs on disk, the four operations over HTTP, and the composition root |
| Outputs | `BimOpenFlow.Publishing`, `.Reports`, `.Dashboards`, `.Evidence` | What a run produces for people |
| Catalog docs | `BimOpenFlow.NodeDocs` | Generates the node reference from the catalog |
| MCP server | `src/mcp/BimOpenMcp.Flow` | The graph operations, evaluation, results, and runs as MCP tools; stdio, or HTTP with `--http <port>` |
| Ask | `src/studio/BimOpenFlow.Ask` | The agent loop behind the editor's Ask box, over any in-process MCP server |
| Editor | `bimopenflow/web/packages/` `graph`, `state`, `panes`, `api-client`, `contracts`, `app`, `viz` | The web editor: a canvas built on Gratify, tables, charts, and the Ask box |
| Agent skill | `.claude/skills/bim-flow/` | How an agent builds and reads graphs; its guide files are also the Ask box's system prompt |

The toolkit's generated catalog (`docs/nodes.catalog.json`) lists 124 node kinds on 2026-10-03. Each declares its ports, parameter kinds, enum values, and whether it is pure or an effect, and the node reference is generated from that, so the catalog is the documentation an agent reads.

Some of it stays in the toolkit: the packs that know about buildings (`Nodes.Bos`, `Nodes.BimAnalysis`), the studio host that composes them, and the 3D pane, which the toolkit plugs into the editor. A small public building shipped as DuckDB tables is planned, so this repository can show BIM questions without any toolkit code.

## The editor

The editor draws the graph on a canvas built with Gratify, marks each node with its evaluation state, and shows the result of the selected node as a table or chart beside it. Its Ask box takes a question in plain language; the agent answers by building or editing a graph through the same four operations, and the finished graph opens in the editor. On 2026-10-03 the toolkit's brief lists what is still missing: the canvas does not yet name the upstream cause of an unready node, and a graph built from Claude Code over MCP needs a page reload to appear.

## What it does not do

- It does not read IFC or BOS by itself. [BIM Open Data](https://github.com/ara3d/bim-open-data) does, and the toolkit's BIM packs connect the two.
- The host is a single-user local process: no accounts, authentication, or hosted service.
- It does not draw 3D. [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer) does, and the toolkit supplies the 3D pane.

## Dependencies

After the move, this repository will read its dependencies from a git-ignored `deps/` folder that `node deps.mjs` fills from `deps.json`, as the viewer already does:

- [`ara3d-dataflow`](https://github.com/ara3d/ara3d-dataflow), the engine: specification, evaluator, expression language, run records, and the conformance suite. It knows nothing about buildings. Whether to fold it into this repository is an open question (Q6 in the toolkit's `docs/proposals/repository-layout.md`).
- [`gratify`](https://github.com/ara3d/gratify), the canvas UI library the editor is drawn with.

## Web page

`site/index.html` is the repository's page, deployed to `https://ara3d.github.io/bim-open-flow/` by `.github/workflows/pages.yml` on each push to `main` that changes `site/`. The owner must first switch Pages on in this repository's settings (Settings, Pages, Source: GitHub Actions); until then the workflow's deploy step fails.

## The family

BIM Open Flow is one of the BIM Open repositories, alongside [BIM Open Data](https://github.com/ara3d/bim-open-data) (the .NET implementation of BIM Open Schema and IFC), [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer), [BIM Open Notebook](https://github.com/ara3d/bim-open-notebook) (agent sessions as documents), and [BIM Open Toolkit](https://github.com/ara3d/bim-open-toolkit), which joins them. The mark and colours follow the toolkit's `docs/BRANDING.md`.

## License

MIT. See `LICENSE`.
