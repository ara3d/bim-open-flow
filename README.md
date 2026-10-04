# BIM Open Flow

![BIM Open Flow](docs/brand/lockup.svg)

Graphs that turn questions about building data into small, inspectable, reproducible computations, built the same way by people in a web editor and by AI agents over MCP.

**Status on 2026-10-03: the code is here, with its history.** Phase 5 of the toolkit's [repository split](https://github.com/ara3d/bim-open-toolkit/blob/main/docs/plans/repository-split.md) moved the generic graph system out of [`ara3d/bim-open-toolkit`](https://github.com/ara3d/bim-open-toolkit), keeping every path as it was: 24 projects of `src/flow`, the MCP server, the Ask loop, their tests, the eight web packages of the editor, and the sample tables and graphs. This repository builds and tests on its own after `node deps.mjs` (see "Build and test"). The toolkit takes it from its own `deps/bim-open-flow` and adds the BIM node packs, the studio host, the 3D pane, and the BIM samples. Some project READMEs and comments inside the moved folders still describe the toolkit's layout; they are corrected as they are touched.

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

## What is here

| Part | Path | Contents |
|---|---|---|
| Contracts | `src/flow/BimOpenFlow.Contracts`, `contracts/` | Wire types shared with the web client, and the generator of their TypeScript twins |
| Node packs | `src/flow/BimOpenFlow.Nodes.*` | One project per vocabulary: tables, table operations, cleaning, dates, DuckDB, relations, spatial, compliance checks, charts and views, effects. `Nodes.Support` holds shared helpers. |
| Relations | `src/flow/BimOpenFlow.Relations`, `.Relations.DuckDb` | Query plans and schemas behind the `rel.*` nodes, and their DuckDB compiler |
| Host | `BimOpenFlow.Host.Catalog`, `.Host.Store`, `.Host.Api`, `BimOpenFlow.Host` | Model discovery, graphs and runs on disk, the four operations over HTTP, and the composition root. `bimopenflow-host` serves the `tables` profile; a host that composes more packs passes its own `HostProfiles` |
| Outputs | `BimOpenFlow.Publishing`, `.Reports`, `.Dashboards`, `.Evidence` | What a run produces for people |
| Graph text and docs | `BimOpenFlow.GraphText`, `BimOpenFlow.NodeDocs` | A graph as readable text, and the node reference generated from the catalog |
| MCP server | `src/mcp/BimOpenMcp.Flow` | The graph operations, evaluation, results, and runs as MCP tools; stdio, or HTTP with `--http <port>` |
| Ask | `src/studio/BimOpenFlow.Ask` | The agent loop behind the toolkit's Ask box, over any in-process MCP server, with Anthropic, OpenAI, and Claude Code command-line backends |
| Editor | `bimopenflow/web/packages/` `contracts`, `api-client`, `state`, `graph`, `viz`, `client`, `panes`, `app` | The web editor: a canvas built on Gratify, table, chart, verdict, and inspector panes, and a registry other panes plug into |
| Samples | `samples/tables`, `samples/analyses`, `samples/relations` | Small CSV, XLSX, SQLite, DuckDB, JSON, and BFAST tables, and the graphs over them that the tests evaluate |
| Tests and gates | `tests/`, `gates/` | NUnit projects per library, a layering test, and the host and web smoke gates CI runs |

The generic host's catalog (`bimopenflow/web/packages/graph/test/nodes.catalog.json`, kept current by `GenericNodeCatalogFileTests`) lists 83 node kinds on 2026-10-03. Each declares its ports, parameter kinds, enum values, and whether it is pure or an effect, and the node reference is generated from that, so the catalog is the documentation an agent reads.

What stays in the toolkit: the packs that know about buildings (`Nodes.Bos`, `Nodes.BimAnalysis`, `Nodes.Geometry`); the studio host, which composes them with these packs and serves the Ask box (`/api/ask`, whose system prompt is the toolkit's `bim-flow` agent skill); the 3D pane, which the toolkit registers with the editor; and the BIM, NRC, and Snowdon samples. A small public building shipped as DuckDB tables is planned (phase 5d), so this repository can show BIM questions without any toolkit code; it waits on whether the Duplex model may be redistributed (the toolkit's TKT-144).

## The editor

The editor draws the graph on a canvas built with Gratify, marks each node with its evaluation state, and shows the result of the selected node as a table or chart beside it. In the toolkit's studio its Ask box takes a question in plain language; the agent answers by building or editing a graph through the same four operations, and the finished graph opens in the editor. The editor served from this repository alone (`npm run web`) has no Ask box, because the endpoint behind it lives in the studio. On 2026-10-03 the toolkit's brief lists what is still missing: the canvas does not yet name the upstream cause of an unready node, and a graph built from Claude Code over MCP needs a page reload to appear.

## What it does not do

- It has no node that knows about buildings, but it does read building files through [BIM Open Data](https://github.com/ara3d/bim-open-data): the host's model catalog (`BimOpenFlow.Host.Catalog`) lists IFC and BOS files and converts IFC to BOS; the DuckDB, relations, cleaning, dates, table-operation, and effect packs run their SQL through BIM Open Schema's DuckDB library; and the IFC write-back effect edits property sets. The nodes that turn a building into tables (`bos.*`, `bim.*`, `view3d.*`) are the toolkit's.
- The host is a single-user local process: no accounts, authentication, or hosted service.
- It does not draw 3D. [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer) does, and the toolkit supplies the 3D pane.

## Dependencies

This repository reads its dependencies from a git-ignored `deps/` folder that `node deps.mjs` fills from `deps.json`, as BIM Open Data and the viewer do:

- [`ara3d-dataflow`](https://github.com/ara3d/ara3d-dataflow), the engine: specification, evaluator, expression language, run records, and the conformance suite. It knows nothing about buildings. Whether to fold it into this repository is an open question (Q6 in the toolkit's `docs/proposals/repository-layout.md`).
- [`bim-open-data`](https://github.com/ara3d/bim-open-data): the BIM Open Schema libraries (DuckDB, I/O, object model), the IFC loader, IFC to BOS, and IFC editing. Its own pins (`bim-open-schema`, `ara3d-sdk`, `parakeet`) land beside it in `deps/`.
- [`gratify`](https://github.com/ara3d/gratify), the canvas UI library the editor is drawn with, read from its source.

When this repository is itself a dependency (the toolkit's `deps/bim-open-flow`), `deps.mjs` links its `deps/*` to the host's copies, and `Directory.Build.props` reaches them by the same paths (`DepsRoot`), so each dependency is checked out and built once.

## Build and test

Windows, the .NET 8 and 10 SDKs, and Node 22:

```
node deps.mjs
dotnet build BimOpenFlow.sln -c Release
dotnet test BimOpenFlow.sln -c Release --no-build --filter "TestCategory!=RequiresTestData"
npm ci --prefix bimopenflow/web
node gates/host-smoke.mjs
node gates/web-smoke.mjs
```

`npm run host --prefix bimopenflow/web` starts the generic host on port 5214, and `npm run web --prefix bimopenflow/web` serves the editor on port 5304 against it. CI (`.github/workflows/build.yml`) runs the same steps.

## Web page

`site/index.html` is the repository's page, deployed to `https://ara3d.github.io/bim-open-flow/` by `.github/workflows/pages.yml` on each push to `main` that changes `site/`. The owner must first switch Pages on in this repository's settings (Settings, Pages, Source: GitHub Actions); until then the workflow's deploy step fails.

## The family

BIM Open Flow is one of the BIM Open repositories, alongside [BIM Open Data](https://github.com/ara3d/bim-open-data) (the .NET implementation of BIM Open Schema and IFC), [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer), [BIM Open Notebook](https://github.com/ara3d/bim-open-notebook) (agent sessions as documents), and [BIM Open Toolkit](https://github.com/ara3d/bim-open-toolkit), which joins them. The mark and colours follow the toolkit's `docs/BRANDING.md`.

## License

MIT. See `LICENSE`.
