# @bimopenflow/graph

The BimOpenFlow graph editor as a component: the gratify canvas that draws a
graph document's nodes, wires, inline parameter controls, status badges, and
wire peeks over a `@bimopenflow/state` store. It can be mounted several times
on one page, each mount with its own state, and a mount can be read-only.

It owns no selection (the store's `selection` is the selection), evaluates
nothing, and talks to no host: a caller that wants row counts or peeks passes
a reader for node outputs.

Plan, chunks, and status: [docs/plans/graph-editor-package.md](../../../../docs/plans/graph-editor-package.md).
Why the canvas is a package of this repository and not part of gratify:
[docs/graph-module-layering.md](../../../../docs/graph-module-layering.md).

## Depends on

`gratify` (the submodule source, through the alias in `tsconfig.json` and
`vitest.config.ts`), `@bimopenflow/state`, and `@bimopenflow/contracts`.
`test/layering.test.ts` fails on an import of the application, the panes, the
host client, the notebook, or gratify's example code.

## Gates

```sh
npm test -w @bimopenflow/graph
npm run typecheck -w @bimopenflow/graph
```

## Status

Skeleton. `createGraphEditor` throws until the canvas modules move in (plan
chunk G4).
