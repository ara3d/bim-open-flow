# site/

The repository's GitHub Pages page, https://ara3d.github.io/bim-open-flow/.

`index.html`, `notice.js`, `assets/`, and `data/` are committed. `app/` is built and git-ignored: `bimopenflow/web/packages/site-web` builds `app/site.js` and `app/site.css`, which mount the real web editor (`@bimopenflow/app`) into `#buildings` with one tab per graph. A visitor pans and zooms the canvas, clicks a node to show its table or chart, and hovers a port to peek at the rows on its wire.

No host stands behind the page, and nothing evaluates in the browser. The editor talks to a static API (`site-web/src/staticApi.ts`), a fetch-shaped function that answers the editor's requests from two files:

- `data/buildings.json`, written by the explicit test `PublicBuildingsSiteData.Write` in `tests/flow/BimOpenFlow.TableWorkflows.Tests`, which evaluates every graph in `samples/buildings` with the generic packs. It holds each graph's document and, per node, its status, row count, columns, and first rows (as many as the file's `sampleRows` says); a table on the page shows those rows under the full row count. The test `SiteData_IsCurrent` fails when the file is stale.
- the node catalog, `bimopenflow/web/packages/graph/test/nodes.catalog.json`, kept current by `GenericNodeCatalogFileTests` and bundled into `app/site.js`.

An edit on the page is kept in memory until the page reloads and changes no result; Run, models, and the Ask box are refused or hidden.

Build and look locally, from `bimopenflow/web` after `node deps.mjs` and `npm ci`:

```sh
npm run build -w @bimopenflow/site-web     # writes site/app
npm run preview -w @bimopenflow/site-web   # serves site/ at http://127.0.0.1:5320
```

`node gates/pages-smoke.mjs` tests and builds the page and checks what Pages serves; `.github/workflows/pages.yml` runs it and uploads `site/`.
