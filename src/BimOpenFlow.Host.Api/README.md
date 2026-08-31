# BimOpenFlow.Host.Api

The HTTP surface of the BimOpenFlow host, generated-contract-first: every
endpoint implements one entry of `contracts/contracts.json` on the generated
`ApiRoutes` templates, and every handler delegates to `BimOpenFlow.Host.Catalog`,
`BimOpenFlow.Host.Store`, or the dataflow engine. No business logic lives here.

Built on ASP.NET Core minimal APIs (framework only, no extra packages).
JSON is camelCase with enums serialized by name, matching the generated
TypeScript contracts exactly.

## Pieces

- `ApiServer` — `Create(catalog, store, registry)` builds the app;
  `MapBimOpenFlowApi` composes the routes onto any endpoint builder.
- `AnalysisSessions` — one standing `EvalSession` per analysis, created on
  demand from the store; per-session lock (the engine is single-threaded);
  SSE fan-out via the session's observers.
- `DocumentEndpoints` — models, analysis CRUD/history, node catalog.
- `EvalEndpoints` — evaluation state, result paging (`skip`/`take`,
  default take 1000; scalar outputs become a one-cell slice), run
  create/list/get, and the `text/event-stream` endpoint
  (`data: <EvalUpdate JSON>\n\n` per evaluation pass, initial state on
  connect, keep-alive comment every 15 s).
- `ApiMapping` — pure host/engine → contract mapping; enum crossings by name.
- `RunInputs` — pins ModelRef/FilePath params by content hash when freezing runs.

## Errors

Missing analysis/model/result → 404 `ApiError`; malformed or invalid
documents → 400 `ApiError`; duplicate run archive → 409 `ApiError`.
