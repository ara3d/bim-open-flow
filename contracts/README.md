# Contracts

Single source of truth for app-level shared types: node-catalog descriptors,
table wire format, and shared enums (parameter kinds, port types, verdicts).

- Source: `contracts.json` (edit this only)
- Codegen: `node contracts/generate.mjs`
- Outputs (committed, never hand-edited):
  - `contracts/generated/csharp/BimOpenFlow.Contracts.g.cs` — compiled into
    `BimOpenFlow.Host.Api` (and anything else needing wire types)
  - `bimopenflow/web/packages/contracts/src/index.ts` — the `@bimopenflow/contracts`
    package, imported by `@bimopenflow/viz`, `@bimopenflow/api-client`, etc.

The engine-level enums (`ParamKind`, `PortType`, `NodeCapability`) are defined
independently in `Ara3D.DataFlowEngine.Abstractions` (the engine takes no
dependency on contracts). A unit test in the host asserts the two stay
member-for-member identical.

The graph document format itself is NOT here — that is `spec/dataflow-graph/`.
The host HTTP API surface will be added here when `BimOpenFlow.Host.Api` lands.
