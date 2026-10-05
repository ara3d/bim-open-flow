# BIM Open Flow

![BIM Open Flow](docs/brand/lockup.svg)

BIM Open Flow is a dataflow engine, a web editor, and an MCP (Model Context Protocol) server for answering questions about tables with small graphs of nodes. It is written for developers who are building tools over building data, and for AI agents that build the graphs on a person's behalf. The building-specific parts live in the companion repository [BIM Open Toolkit](https://github.com/ara3d/bim-open-toolkit); this repository holds everything that does not need to know what a wall is.

**Maturity, 2026-10-05:** experimental and about five weeks old (first commit 2026-08-30, 390 commits since). It has one author and one serious user, the toolkit. The code was moved here from the toolkit on 2026-10-03 with its history, and a few project READMEs inside the moved folders still describe the toolkit's layout.

## The problem it solves

Answering a question about a building model ("how many spaces per storey?", "which doors are narrower than 850 mm?") usually means a query someone types into a database shell or a script against an authoring tool's API. The answer is a number in a chat window or a screenshot. Nobody can see how it was produced, rerun it next month on a new export, or hand it to a colleague who does not have the tool.

BIM Open Flow stores the computation instead of the answer. A question becomes a graph of a handful of nodes, saved as a JSON document. The same graph can be drawn by a person in the editor, built by an AI agent through the MCP server, evaluated for display without side effects, and replayed later from a run record that pins every input by content hash.

## What it does not do

- It has no node that knows about buildings. The packs that turn an IFC (Industry Foundation Classes) or BOS (BIM Open Schema) file into tables, and the 3D pane, belong to the toolkit. This repository reads building files only through [BIM Open Data](https://github.com/ara3d/bim-open-data), as DuckDB databases of tables.
- It does not draw 3D geometry. [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer) does.
- It is not a hosted service. The host is a single-user local process with no accounts and no authentication.
- It is not a general scripting environment. Every node is a C# class in a pack; adding one needs a build. A script-defined node is an open question in the toolkit (its ticket TKT-162).
- It does not run on Linux or macOS. The host carries the native web-ifc library through the IFC loader, so the .NET solution builds on Windows only. The web editor and the landing page are portable.

## What a graph looks like

This graph, `samples/buildings/schependomlaan-room-names.json`, counts the rooms in a Dutch apartment block by name. It reads one DuckDB table, keeps the rows whose category is `IFCSPACE`, groups them by name, sorts, and draws a bar chart. The `layout` section, which only places the nodes on the canvas, is left out here.

```json
{
  "formatVersion": "0.1.0",
  "structure": {
    "nodes": [
      { "id": "entities", "kind": "duck.table", "version": 1 },
      { "id": "spaces", "kind": "table.filter", "version": 1 },
      { "id": "byName", "kind": "table.aggregate", "version": 1 },
      { "id": "sorted", "kind": "table.sort", "version": 1 },
      { "id": "chart", "kind": "chart.bar", "version": 1 }
    ],
    "edges": [
      { "from": "entities.table", "to": "spaces.table" },
      { "from": "spaces.table", "to": "byName.table" },
      { "from": "byName.table", "to": "sorted.table" },
      { "from": "sorted.table", "to": "chart.table" }
    ]
  },
  "values": {
    "entities": { "path": "{PUBLIC}/schependomlaan.duckdb", "table": "EntityText" },
    "spaces": { "expr": "Category == 'IFCSPACE'" },
    "byName": { "aggregates": "count(EntityIndex) as Spaces", "groupBy": "Name" },
    "sorted": { "A": "Spaces", "B": "Name", "descendingA": "true" },
    "chart": { "labelColumn": "Name", "title": "Spaces by name", "valueColumns": "Spaces" }
  }
}
```

Evaluated over the sample data, the `sorted` node outputs 15 rows for 100 spaces. The two most common names are `entree` and `instal. ruimte`, 11 each; `badkamer`, `keuken`, `mk`, `toilet`, and `woonkamer` have 10 each, one per apartment. The same graph and five others are shown with their results, without any server, on the repository's page at <https://ara3d.github.io/bim-open-flow/>.

An agent builds the same graph by calling the MCP server's tools in order: `addNode` five times, `connect` four times, `setParam` for each value, then `evaluate` and `getResult`. Every one of those calls goes through the same four edit operations the editor uses, so a graph an agent builds is one a person could have drawn.

## How to use it

### Prerequisites

- Windows. See the Linux and macOS note above.
- The .NET 8 SDK. Every project in the solution targets .NET 8; CI also installs the .NET 10 SDK, which no project here needs.
- Node.js 22, for the dependency script, the web editor, and the gates.
- Git, which the dependency script calls.

### Build and test

The repository reads its three dependencies from a git-ignored `deps/` folder, which `node deps.mjs` fills from the commits pinned in `deps.json`. The commands below were run on 2026-10-05: the build finished with 0 errors, and 1,044 tests passed with 2 skipped.

```
node deps.mjs
dotnet build BimOpenFlow.sln -c Release
dotnet test BimOpenFlow.sln -c Release --no-build --filter "TestCategory!=RequiresTestData"
npm ci --prefix bimopenflow/web
node gates/host-smoke.mjs
node gates/web-smoke.mjs
```

`host-smoke` starts the real host, drives its HTTP API through a graph end to end, and prints `HOST SMOKE: PASS`. `web-smoke` tests and type-checks every web package and builds the editor. CI (`.github/workflows/build.yml`) runs the same steps on every push.

### Run the editor

```
npm run host --prefix bimopenflow/web
npm run web --prefix bimopenflow/web
```

The first command starts the host on port 5214 with the `tables` profile and seeds its store with the six graphs in `samples/analyses` over the CSV, Excel, SQLite, and DuckDB files in `samples/tables`. The second serves the editor on port 5304 against it. The editor draws the graph on a canvas, marks each node with its evaluation state, and shows the selected node's output as a table or chart. It has no Ask box; the natural-language entry point lives in the toolkit's studio, which has the endpoint behind it.

### Register the MCP server with an agent

The MCP server speaks stdio by default, which is how MCP clients launch a server, or HTTP with `--http <port>`. After the Release build, this `.mcp.json` entry registers it with Claude Code, pointed at the two public buildings that `node deps.mjs` places under `deps/bim-open-data/samples/public`:

```json
{
  "mcpServers": {
    "bimopenflow": {
      "command": "dotnet",
      "args": [
        "src/mcp/BimOpenMcp.Flow/bin/Release/net8.0-windows/bimopenmcp-flow.dll",
        "--models", "deps/bim-open-data/samples/public",
        "--store", "artifacts/flow/store",
        "--cache", "artifacts/flow/cache"
      ]
    }
  }
}
```

The server's tools fall into five groups: models and databases (`listModels`, `listDatabases`, `describeDatabase`), documents (`listAnalyses`, `getAnalysis`, `saveAnalysis`), edits (`addNode`, `connect`, `setParam`, `removeNode`, and `editGraph` for a batch), evaluation (`evaluate`, `getResult`, `createRun`, `listRuns`, `getNodeCatalog`), and the repository's own documentation (`readDoc`, `searchDocs`). The toolkit's `.claude/skills/bim-flow/` holds the guide an agent reads before its first call.

## How it works

**A graph** is a JSON document of nodes and the wires between them. Each node is a small function with typed input and output ports and parameters set on the node itself. Six kinds of value travel on wires: Boolean, Integer, Number, Text, Table, and Relation. Almost everything useful is an immutable Table. A Relation is a query plan with its schema, not rows; the rows exist only once a node executes the plan. A new kind of input is a new parameter kind, never a new wire type, so one vocabulary serves loading, SQL, table operations, charts, and checks.

**Four operations** edit a graph: `addNode`, `connect`, `setParam`, and `removeNode`. They back the HTTP API, the MCP tools, and every gesture in the editor. A whole document written at once is validated as if it had been built from them.

**Pure nodes and effects.** A pure node only computes; its results are memoized and recomputed when an input changes. An effect node writes something: a CSV, Excel, or Parquet file, a report, or property sets back into an IFC file. Every effect node lives in one pack, `BimOpenFlow.Nodes.Effects`, so purity is enforced by project reference. Evaluating a graph for display never runs an effect; the node reports `EffectPending`. Effects run only inside an explicit Run. A person or an agent exploring a model cannot touch a file by accident.

**Every node reports a state**: `Ok`, `Unready` (an input is missing), `EffectPending`, `Unavailable`, or `Error`. A half-wired graph is a normal thing to look at and repair.

**Runs are the evidence.** A run record stores the hash of the graph and the content hash of every input beside the outputs, and can be replayed. The publishing projects turn a run into a self-contained HTML report, a dashboard, or an evidence package: a zip whose `manifest.json` lists a SHA-256 per member.

**The catalog is the documentation.** Each of the 83 node kinds in the generic profile (counted on 2026-10-05 in `bimopenflow/web/packages/graph/test/nodes.catalog.json`, which a test keeps current) declares its ports, parameter kinds, enum values, and whether it is pure or an effect. The node reference is generated from that, and so is the description an agent reads.

## Trade-offs

- **One edit path, so no scripting shortcut.** Routing every edit through four operations keeps the editor, the HTTP API, and the agent in step. The cost is that building a large graph is many small calls, and there is no way to express a loop or a conditional inside a graph.
- **Tables as the currency.** Treating a scalar as a parameter rather than a value on a wire keeps the node vocabulary small; on 2026-10-05 the 83 nodes use only Table, Text, and Relation ports. The cost is that a count computed by one node reaches another as a one-row table, not as a number.
- **Effects isolated by project reference.** Purity is checked by the compiler and a layering test rather than by annotation, which is hard to get wrong. The cost is that a node that both reads and writes has to be split in two.
- **Dependencies by pinned commit, not packages.** `deps.json` pins three sibling repositories by commit and the build reads their source. A change in the engine lands here on the day it is made, with no package publishing step. The cost is a `node deps.mjs` before every build and a second checkout of each dependency on disk.
- **C# nodes only.** A node is a class with declared ports, so the catalog, the editor, and the agent's tool description are all generated from one declaration. The cost is a host build for every new node.

## What is tested and what is not

Demonstrated on 2026-10-05:

- The .NET build, the 1,044 tests, and the host smoke gate pass on a Windows machine with the pinned dependencies.
- The six graphs in `samples/buildings` evaluate with every node `Ok` over two public buildings, and `PublicBuildingsTests` checks the numbers listed in `samples/buildings/README.md`. The same results are on the landing page.
- The six table graphs in `samples/analyses` evaluate green over the sample files, checked by the `TableWorkflows` tests.
- The MCP server's tools, the Ask loop over a scripted chat backend, and the graph-to-text rendering have their own test projects (`tests/mcp`, `tests/studio`, `tests/flow/BimOpenFlow.GraphText.Tests`).

Not demonstrated here:

- Performance on a large model. The toolkit's brief sets a budget for a model of about 450,000 instances, and that work and its measurement happen there, not in this repository.
- Any agent other than Claude driving the MCP server. The toolkit's Ask box has run with Anthropic, OpenAI, and Claude Code command-line backends; the generic server here has been exercised by its tests and by Claude Code.
- Writing property sets back into an IFC file through an effect node, outside its unit tests.
- The editor in a real browser by automation. Its tests run under vitest with jsdom, and the host smoke gate covers the HTTP API, not the canvas.

## Related work

The category is not new. These are the closest tools, with how this one differs, checked on 2026-10-05; comparisons like these go stale.

- **[Dynamo](https://dynamobim.org/) and [Grasshopper](https://www.grasshopper3d.com/)** are visual programming environments inside Revit and Rhino. They are mature, have thousands of nodes, and are the tools most BIM professionals already know. They run inside an authoring tool and operate on its live model; BIM Open Flow runs outside any authoring tool over an exported file, and is meant to be driven by an agent as much as by a person.
- **[IfcSverchok](https://github.com/IfcOpenShell/IfcOpenShell)**, part of IfcOpenShell, is a node add-on for Blender that creates and reads IFC through Sverchok nodes. It is geometry-first and lives in Blender; this project is table-first and has no geometry nodes.
- **[Speckle Automate](https://speckle.systems/use-cases/automate)** runs functions over Speckle's hosted model data on change, schedule, or request, with prebuilt checks. It is a hosted service with accounts and connectors to many authoring tools; this project is a local process over a file.
- **[KNIME](https://www.knime.com/)**, [Node-RED](https://nodered.org/), and similar general dataflow tools cover table workflows well, and KNIME has published an MCP server pattern. They know nothing about BIM and carry a much larger runtime; this project is a small engine whose node packs are written for a building toolkit, with runs that pin their inputs by hash.
- **A SQL shell over the DuckDB export** is the baseline the toolkit measures against. It is faster to start and needs no editor, but leaves no replayable record and no picture of the computation.

What this repository claims as its own is narrow: one edit path shared by the editor and the MCP server, effects that cannot run outside an explicit Run, and run records that replay from content hashes.

## How the repository is organized

| Path | Contents |
|---|---|
| `src/flow/BimOpenFlow.Contracts`, `contracts/` | Wire types shared with the web client, and the generator of their TypeScript twins |
| `src/flow/BimOpenFlow.Nodes.*` | One project per vocabulary: tables, table operations, cleaning, dates, DuckDB, relations, spatial, compliance checks, charts, effects. `Nodes.Support` holds shared helpers |
| `src/flow/BimOpenFlow.Relations`, `.Relations.DuckDb` | Query plans and schemas behind the `rel.*` nodes, and their DuckDB compiler |
| `src/flow/BimOpenFlow.Host*` | Model discovery (`Host.Catalog`), graphs and runs on disk (`Host.Store`), the four operations over HTTP (`Host.Api`), and the composition root (`Host`), which serves the `tables` profile as `bimopenflow-host` |
| `src/flow/BimOpenFlow.Publishing`, `.Reports`, `.Dashboards`, `.Evidence` | What a run produces for people |
| `src/flow/BimOpenFlow.GraphText`, `.NodeDocs` | A graph as readable text, and the node reference generated from the catalog |
| `src/mcp/BimOpenMcp.Flow` | The MCP server, `bimopenmcp-flow` |
| `src/studio/BimOpenFlow.Ask` | The agent loop behind the toolkit's Ask box, over any in-process MCP server, with Anthropic, OpenAI, and Claude Code command-line backends |
| `bimopenflow/web/packages/` | The editor as eight npm workspaces: `contracts`, `api-client`, `state`, `graph`, `viz`, `client`, `panes`, `app`. The canvas is built on [Gratify](https://github.com/ara3d/gratify) |
| `samples/tables`, `samples/analyses`, `samples/relations` | Small CSV, Excel, SQLite, DuckDB, JSON, and BFAST tables, and the graphs over them that the tests evaluate |
| `samples/buildings` | Six graphs over two public buildings, with the numbers they produce |
| `site/` | The landing page, deployed to GitHub Pages by `.github/workflows/pages.yml`; its results are precomputed by a test, not evaluated in the browser |
| `tests/`, `gates/` | NUnit projects per library, a layering test, and the two smoke gates |
| `deps.json`, `deps.mjs` | The pinned dependencies and the script that fetches them |

The dependencies are [`ara3d-dataflow`](https://github.com/ara3d/ara3d-dataflow), the engine (specification, evaluator, expression language, run records, and conformance suite); [`bim-open-data`](https://github.com/ara3d/bim-open-data), the BIM Open Schema libraries and the IFC loader, which brings its own pins; and [`gratify`](https://github.com/ara3d/gratify), the canvas library. When this repository is itself a dependency, as the toolkit's `deps/bim-open-flow`, `deps.mjs` links its `deps/*` to the host repository's copies so each one is checked out and built once.

## Who it is for

It is for you if you are:

- a developer building a tool over building data who wants a graph engine, an editor, and an MCP server that already agree with each other;
- a developer adding a node pack, a pane, or an MCP tool to the BIM Open family;
- an agent author who wants a tool surface where exploration cannot write files.

It is not for you yet if you are:

- a BIM professional with a model and a question. Use the [toolkit](https://github.com/ara3d/bim-open-toolkit), which adds the building nodes, the studio with its Ask box, and the 3D pane;
- someone who needs it on Linux or macOS;
- someone who needs more than one user on one host.

## The family

BIM Open Flow is one of the BIM Open repositories, with [BIM Open Data](https://github.com/ara3d/bim-open-data) (the .NET implementation of BIM Open Schema and IFC), [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer), [BIM Open Notebook](https://github.com/ara3d/bim-open-notebook) (agent sessions as documents), and [BIM Open Toolkit](https://github.com/ara3d/bim-open-toolkit), which joins them. The mark and colours follow the toolkit's `docs/BRANDING.md`.

## License and help

MIT; see `LICENSE`. The sample buildings keep their own licences, listed in `samples/buildings/README.md`. Report problems as issues on this repository.
