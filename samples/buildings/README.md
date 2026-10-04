# Sample graphs over public buildings

Six graphs that answer questions about two real buildings with the generic table nodes only. The buildings come from [BIM Open Data](https://github.com/ara3d/bim-open-data)'s `samples/public`, which `node deps.mjs` puts at `deps/bim-open-data/samples/public`: IFC files under open licences, converted to BIM Open Schema tables in DuckDB. The graphs read the DuckDB views `EntityText` (one row per IFC entity, with its `Category`, the IFC class in capitals) and `StoreyOfElement` (each element placed on a storey, with `StoreyName`).

`index.json` lists the graphs, the sample each one reads, and the question it answers. It also names the data folder and the placeholder the graphs use for it, `{PUBLIC}`, so the documents hold no machine-local path. The host does not seed these graphs; open one in the editor by replacing `{PUBLIC}` with the absolute path of `deps/bim-open-data/samples/public`.

| Graph | Data | Nodes | Result |
|---|---|---|---|
| `schependomlaan-elements-by-category` | `schependomlaan.duckdb` | `duck.table` x2, `table.join`, `table.aggregate`, `chart.bar` | 3,721 elements on a storey in 16 IFC classes: coverings 1,262, walls 652, standard-case walls 282, slabs 279, building element parts 277, windows 259, doors 205, beams 174, spaces 100, railings 90, and six smaller classes |
| `schependomlaan-spaces-per-storey` | `schependomlaan.duckdb` | `duck.table` x2, `table.filter`, `table.join`, `table.aggregate`, `table.sort`, `chart.bar` | 100 spaces: `00 begane grond` 32, `01 eerste verdieping` 29, `02 tweede verdieping` 20, `03 derde verdieping` 19 |
| `schependomlaan-room-names` | `schependomlaan.duckdb` | `duck.table`, `table.filter`, `table.aggregate`, `table.sort`, `chart.bar` | 15 distinct names over 100 spaces; `entree` and `instal. ruimte` 11 each, then `badkamer`, `keuken`, `mk`, `toilet`, and `woonkamer` 10 each, one per apartment |
| `digitalhub-openings-per-storey` | `digitalhub-arc.duckdb` | `duck.table` x2, `table.filter`, `table.join`, `table.pivot`, `table.sort`, `chart.bar` | doors, windows, spaces per storey: `B01_OKRD` 13, 0, 12; `E00_OKRD` 26, 21, 26; `E01_OKRD` 25, 26, 26 (64 doors, 47 windows, 64 spaces) |
| `digitalhub-heating-per-storey` | `digitalhub-hzg.duckdb` | `duck.query`, `chart.bar` | pipe segments, fittings, space heaters, valves per storey: `B01_OKRD` 157, 110, 0, 25; `E00_OKRD` 373, 310, 29, 20; `E01_OKRD` 384, 323, 34, 20 |
| `digitalhub-federated-models` | `digitalhub-federated.duckdb` | `duck.query`, `chart.bar` | elements on a storey per discipline model: architecture 777, heating 1,795, ventilation 1,310, plumbing 1,010, three storeys each |

The totals agree with bim-open-data's `samples.json` where both count the same thing: 100 spaces, 205 doors, and 259 windows in Schependomlaan; 64 doors, 47 windows, and 64 spaces in DigitalHub's architecture. Storey names are Dutch in Schependomlaan (`begane grond` is the ground floor) and level codes in DigitalHub (`B01` basement, `E00` ground floor, `E01` first floor); bim-open-data's `samples/public/README.md` has a glossary.

Two choices behind the numbers. The element counts take only entities placed on a storey, which leaves out geometry, property, and type entities; Schependomlaan has 38,947 entities in all. The pivot counts rows, so a storey with no windows shows 0, not an empty cell.

## Tests and the web page

`PublicBuildingsTests` in `tests/flow/BimOpenFlow.TableWorkflows.Tests` validates every graph against the generic packs, evaluates it with every node `Ok`, and checks the numbers above. Without the data in `deps/` the tests are skipped with the reason. `bimopenflow/web/packages/graph/test/buildingSamples.test.ts` checks that the cards neither overlap nor overflow in the editor.

The landing page (`site/`) shows these graphs and their results without a server. The explicit test `PublicBuildingsSiteData.Write` evaluates them and writes `site/data/buildings.json`, and copies bim-open-data's `NOTICE.md` beside it; `SiteData_IsCurrent` and `SiteNotice_IsCurrent` fail when either copy falls behind. After changing a graph or the pin:

```
dotnet test tests/flow/BimOpenFlow.TableWorkflows.Tests -c Release --filter "FullyQualifiedName~PublicBuildingsSiteData"
```

## Licences

The building data keeps its own licence; `deps/bim-open-data/samples/public/NOTICE.md` has the full text, and anything that ships results from these graphs ships that notice too.

- Schependomlaan dataset, (C) original owners, licensed CC BY 4.0, https://creativecommons.org/licenses/by/4.0/; converted to BIM Open Schema tables by Ara 3D.
- DigitalHub, MIT License, Copyright (c) 2020 RWTH Aachen University - E3D Institute of Energy Efficiency and Sustainable Building; converted to BIM Open Schema tables by Ara 3D.

The Duplex Apartment in the same folder is not used here.
