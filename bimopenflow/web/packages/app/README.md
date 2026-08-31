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
- `paneChoice.ts` / `paneArea.ts` / `paneContext.ts` / `paramsPane.ts` — pane
  heuristics per node kind, the tab strip + single active pane, the
  `PaneContext` bridging `requestTable` to `getResult`, and the app-owned
  editable params form. Full docking is deferred by design (its right home is
  gratify — see `docs/bimopenflow-structure.md`).
- `sidebar.ts` / `topbar.ts` / `toast.ts` — chrome: analysis list, catalog
  search, picker/save/run/connection status, notifications.
- `app.ts` — the controller wiring all of the above around one `ApiClient`.

gratify is consumed from the submodule source via a vite/tsc alias to
`submodules/gratify/src/gratify` (pattern copied from `platoflow/web`).

## Assumptions and stubs

- 3D model URLs: `resolveAsset("model:{id}")` maps to
  `/api/models/{id}/geometry.bos` — a stub until the host serves geometry
  (TODO markers in `paneContext.ts` / `paneArea.ts`); the 3D pane currently
  receives only the `instances` table.
- "New" creates an analysis by `PUT`-ing an empty document (the API has no
  dedicated create endpoint).

Provenance: new for the BimOpenFlow rewrite (see `docs/bimopenflow-structure.md`).
