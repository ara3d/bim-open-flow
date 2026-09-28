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

## Layout

Flat `src/`, one concern per file, the names the studio used:

- `canvasEditor.ts` mounts the gratify runtime on a `<canvas>` and syncs it with the store; `canvasIntents.ts` is the intent vocabulary and update function; `canvasParts.ts` the surface, node, wire, and rubber-wire parts.
- `viewModel.ts` turns store state and the node catalog into what the canvas draws; `canvasSlots.ts`, `slotRegistry.ts`, `slotShared.ts`, `canvasControls.ts`, `canvasLongSlot.ts`, `longValueEditor.ts`, `graphWidgets.ts` are the inline parameter controls.
- `portResults.ts`, `portGeometry.ts`, `portHover.ts`, `peekCard.ts`, `peekWiring.ts` are wire row counts and peeks.
- `nodeBadge.ts`, `upstreamEdges.ts`, `selectionBorder.ts`, `nodeContextMenu.ts`, `autoLayout.ts`, `graphPreview.ts`, `canvasTheme.ts` and the small text helpers.
- `gratifyWidgets.ts`, `gratifyRangeMath.ts`: copies of gratify's example slider and range, planned debt until TKT-23.

## Status

Building apart: a copy of the studio's canvas cluster at commit 3e4a699 while
the Editor UX wave and TKT-12 keep editing the studio's copy. The plan's G7
replaces the studio's copy with this package. Per-instance state and the
read-only mode land here first.
