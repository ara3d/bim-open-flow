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

- `shell.ts` / `styles.ts` — the DOM layout (topbar, sidebar, canvas,
  splitter, pane area) under the `bof-app-` class/custom-property prefix.
- `viewModel.ts` — pure store-state → canvas model (positions from the layout
  layer, deterministic defaults for unplaced nodes, ports from the catalog).
- `canvasIntents.ts` — the canvas intent vocabulary and the gratify update
  function: gestures become store dispatches; only mid-drag positions and the
  selected wire are transient canvas state.
- `canvasParts.ts` / `canvasEditor.ts` — gratify parts (surface, node, wire,
  rubber wire; adapted from gratify's node-editor example) and the mount +
  store-subscription sync.
- `slotShared.ts` — island plumbing (dispatch, row key, styling) shared by
  every on-node parameter control.
- `slotRegistry.ts` — maps each parameter row's control to the gratify
  element that draws it.
- `canvasLongSlot.ts` — the on-node row for Json, Expression, and long Text:
  a preview that opens the anchored editor.
- `longValueEditor.ts` — the plain-DOM textarea editor the long-text row
  opens, with its own commit/discard rules.
- `paneChoice.ts` / `paneArea.ts` / `paneContext.ts` — pane heuristics per
  node kind, the tab strip + single active pane, and the `PaneContext`
  bridging `requestTable` to `getResult`. Full docking is deferred by design
  (its right home is gratify — see `docs/bimopenflow-structure.md`).
- `defaultShown.ts` — which node the panes show when nothing is selected
  (TKT-46): the last shown node if it still exists, else the best terminal
  node in the document.
- `sidebar.ts` / `topbar.ts` / `toast.ts` — chrome: analysis list, catalog
  search, picker/save/run/connection status, notifications.
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
  joins), mounted in the sidebar's Steps section; a click selects the node
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

gratify is consumed from the submodule source via a vite/tsc alias to
`submodules/gratify/src/gratify` (pattern copied from the former `platoflow/web`).

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
