# @bimopenflow/state

Client-side store and reducer for the BimOpenFlow editor: a TypeScript mirror
of the graph document, selection, undo/redo, and plumbing for evaluation
updates from the host. Plain TypeScript with no UI framework; testable
headless with vitest.

## Pieces

- `document.ts` — `GraphDocument`, the mirror of the frozen four-layer format
  (`structure` nodes/edges, `values` as canonical strings, `layout`,
  free-form `session`), plus `parseDocument` / `serializeDocument` /
  `parsePortRef`.
- `actions.ts` / `reducer.ts` — the action union and pure `reduce(state, action)`.
- `store.ts` — `createStore()` returning `{getState, dispatch, subscribe}`.
  `dispatch` is the single choke point for every graph mutation (design
  principle P2); listeners are notified at most once per dispatch, and only
  when the state changed.
- `sync.ts` — `connectAnalysis(store, api, analysisId)`: loads the document
  and evaluation state, wires the server-sent event stream into
  `applyServerState`, and returns `{save, dispose}`. It takes the structural
  `AnalysisApi` interface (a subset of the generated `ApiClient`), so tests
  fake the whole API with a plain object and no `EventSource`.

## State shape

```ts
{
  document: GraphDocument,
  selection: string[],          // node ids; never part of undo history
  evalState: Record<string, NodeState>,  // merged from EvalUpdate messages
  dirty: boolean,               // document changed since load/save
  undoStack: string[],          // serialized document snapshots
  redoStack: string[],
}
```

Actions: `addNode`, `removeNode`, `connect`, `disconnect`, `setParam`,
`setLayout`, `select`, `clearSelection`, `undo`, `redo`, `applyServerState`,
`setDocument`, `markSaved` (internal, dispatched by `save()`).

Edit semantics mirror `Ara3D.NodeGraph`: `removeNode` drops the node's edges,
values, and layout; `connect` replaces any existing edge into the target input
port; invalid edits throw and leave the state unchanged. Undo/redo is a
snapshot stack of serialized documents; `setDocument` (a load, not an edit)
resets history, selection, eval state, and the dirty flag.

## Canonicalization boundary

`serializeDocument` follows the canonical rules where cheap: keys sorted at
every level, nodes sorted by id, edges by `to`, 2-space indent, LF lines with
one trailing LF, empty `layout`/`session` omitted. Byte-identity with the
server's canonical writer is NOT guaranteed (notably shortest-round-trip
doubles); the server re-canonicalizes on save, and graph hashing is
server-side only. The client's job is to round-trip the JSON it received and
apply edits structurally.

## Commands

```
npm test -w @bimopenflow/state
npm run typecheck -w @bimopenflow/state
```

Provenance: new for the BimOpenFlow rewrite (see `docs/bimopenflow-structure.md`).
