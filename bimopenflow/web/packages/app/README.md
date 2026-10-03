# @bimopenflow/app

The BimOpenFlow editor shell: graph canvas editing on gratify, node catalog
browsing, one pane area with tabs, and session/run controls. Owns the `layout`
and `session` layers of a graph file; never evaluates anything itself — every
graph mutation goes through the `@bimopenflow/state` store (the single
mutation path), and all data comes from the host via `@bimopenflow/api-client`.

## Running

```sh
cd bimopenflow/web
npm install
npm run dev -w @bimopenflow/app     # http://localhost:5300
```

The dev server proxies `/api` to the host at `http://127.0.0.1:5214`; override
the target with the `BOF_HOST` environment variable:

```sh
BOF_HOST=http://127.0.0.1:5999 npm run dev -w @bimopenflow/app
```

The app itself always talks to the same origin (`baseUrl: ""`), so a
production build works served from the host directly. Without a reachable
host the shell still loads and reports "offline"; there is nothing to edit
until an analysis can be opened.

Gates:

```sh
npx tsc -p packages/app --noEmit     # typecheck
npm test -w @bimopenflow/app        # vitest (jsdom)
npm run -w @bimopenflow/app build   # vite production build
```

## Structure

The controller (`app.ts`) and the look are separate. `chrome.ts` is the
seam: a chrome provides the slots the shared content mounts into (canvas,
pane area, Ask host, step list) and a few setters (analyses, dirty,
connection, catalog, nodes), and raises `ChromeActions` (open, new, save,
run, fit, tidy, add node, select, show in pane, theme, node style). Two
chromes exist:

- `classicChrome.ts` — `shell.ts` / `styles.ts` (the DOM layout under the
  `bof-app-` class/custom-property prefix), `topbar.ts`, `sidebar.ts`, and
  the graph demo's toolbar. `index.html`, `3d.html`, and `duckdb.html` use it.
- `studio/studioChrome.ts` + `studio/studio.css` — the studio look at
  `/studio.html` (`studio.ts`): a command bar with the flow's title
  (`studio/flowTitle.ts`), a primary Run, a View menu, a floating canvas
  toolbar (Fit, Tidy, Add node), an empty-flow card, a gestures popover, and
  the Ask box over the panes. It reuses the sidebar, step list, problems
  strip, start page, Ask panel, and pane area unchanged and restyles their
  classes under `.bof-studio`; it keeps its own canvas theme choice
  (`studio`, the light palette with a stripe per node pack) and hides the
  canvas's own hint line. A third look is one more factory passed as
  `AppOptions.chrome`.

`columnSplitter.ts` holds the ghost-line column splitters both layouts use;
`bootEditor.ts` boots either page over the same-origin host.


- The graph canvas (view model, intents, parts, inline controls, peeks, node
  styles, theme) is `@bimopenflow/graph` (`packages/graph`), mounted here by
  `app.ts` through `createGraphEditor`; `themeChoice.ts` and
  `nodeStyleChoice.ts` persist the studio's choices for it.
- `paneChoice.ts` / `paneArea.ts` / `paneContext.ts` — pane heuristics per
  node kind, the tab strip + single active pane, and the `PaneContext`
  bridging `requestTable` to `getResult`. Full docking is deferred by design
  (its right home is gratify — see `docs/bimopenflow-structure.md`).
- `defaultShown.ts` — which node the panes show when nothing is selected
  (TKT-46): the last shown node if it still exists, else the best terminal
  node in the document.
- `sidebar.ts` / `topbar.ts` / `toast.ts` — chrome: the sidebar's Steps and Nodes tabs
  (TKT-116), the flow picker, save/run/connection status, notifications.
- `peekWiring.ts` — connects the port-results controller, evaluation watcher,
  and hover listener to a canvas; `createCanvasEditor` uses it when given a
  `readPort` (TKT-11: hover a socket or wire for its rows, counts on wires).
- `startPage.ts` / `templates.ts` — the start page: one card per sample flow
  grouped by folder (Open, Copy, Blank flow; dimmed when the host profile does
  not seed it). The catalog `templates.generated.ts` comes from
  `node scripts/build-flow-templates.mjs` (`--check` in CI); rerun it after
  adding or describing a sample (TKT-14).
- `stepList.ts` / `graphOrder.ts` — the open flow as numbered steps in
  dataflow order (title, parameter summary, status, row count, "from 1, 2" for
  joins), mounted in the sidebar's Steps tab; a click selects the node
  (TKT-95).
- `canvasPalette.ts` / `paletteFilter.ts` / `addNodePlan.ts` — right-click
  empty canvas, or drop a wire there, to open the node palette; a dropped wire
  lists only kinds with a port that can take it, and each pick adds, places,
  selects, and connects the node as one undo step (the store's `batch`
  action) (TKT-96).
- `problemsPanel.ts` / `graphProblems.ts` — a strip along the bottom of the
  canvas that reads "N problems · M errors" and expands to every non-Ok node,
  root causes first (TKT-97).
- `nodeStyle.ts` / `nodeStyleChoice.ts` / `nodeRender.ts` — the node card
  style switch (classic, banner, chip, bar; topbar "Node style", saved under
  `bof-app-node-style`) and the card drawing, with a pure `nodeCardLayout`
  that records where the title, id, description, and status go in each style
  (TKT-98).
- `app.ts` — the controller wiring all of the above around one `ApiClient`.

gratify is consumed from the `deps/gratify` source via a vite/tsc alias to
`deps/gratify/src/gratify` (pattern copied from the former `platoflow/web`).

## Assumptions and stubs

- 3D model URLs: `resolveAsset("model:{id}")` maps to
  `/api/models/{id}/bos` — a stub until the host serves geometry
  (TODO markers in `paneContext.ts` / `paneArea.ts`); the 3D pane currently
  receives only the `instances` table.
- "New" creates an analysis by `PUT`-ing an empty document (the API has no
  dedicated create endpoint).

Provenance: new for the BimOpenFlow rewrite (see `docs/bimopenflow-structure.md`).
# Snowdon 3D graphs

Open **Snowdon 3D graphs** in the editor's top bar, or visit `/3d.html`.
The editable graph opens on the left with its live 3D preview on the right.
Edit node fields, connect sockets, and select a node to preview its result.
The standalone button examples remain at `/showcase.html`.
See [setup and examples](../../../../docs/bim-flow-3d.md).
