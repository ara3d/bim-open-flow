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

- `canvasEditor.ts` mounts the gratify runtime on a `<canvas>` and syncs it with the store (`createGraphEditor`, its options, `fit`, `setTheme`, `results`); `instance.ts` is the state one mount owns (islands, editors, dropdown flags, dispatch, suggestion provider); `canvasIntents.ts` is the intent vocabulary and update function, with the read-only guard; `canvasParts.ts` the surface, node, wire, and rubber-wire parts, and `nodeRender.ts` / `nodeStyle.ts` the node card in its four styles.
- `viewModel.ts` turns store state and the node catalog into what the canvas draws; `canvasSlots.ts`, `slotRegistry.ts`, `slotShared.ts`, `canvasControls.ts`, `canvasLongSlot.ts`, `longValueEditor.ts`, `graphWidgets.ts` are the inline parameter controls.
- `portResults.ts`, `portGeometry.ts`, `portHover.ts`, `peekCard.ts`, `peekWiring.ts` are wire row counts and peeks.
- `nodeBadge.ts`, `upstreamEdges.ts`, `selectionBorder.ts`, `nodeContextMenu.ts`, `autoLayout.ts`, `graphPreview.ts`, `canvasTheme.ts` and the small text helpers.
- `gratifyWidgets.ts`, `gratifyRangeMath.ts`: copies of gratify's example slider and range, planned debt until TKT-23.

## Read-only mode

`createGraphEditor(canvas, { store, catalog, onError, readOnly: true })` gives a
viewer: it pans, zooms, hovers, and selects, and nothing changes the document.
`makeCanvasUpdate` drops every mutating intent, and the parts hide the gestures
(no wire drag, no move, no dropdown, disabled inputs, no context menu).

## Page-wide state that remains

The canvas theme (gratify's tokens are process-wide) and the node card style
(`nodeStyle.ts`) are page-wide by design: `setTheme` on any editor changes every
canvas on the page. A per-runtime theme needs a token set in gratify's runtime.

## Status

Built (TKT-94, 2026-09-28): the studio mounts this package and the notebook
mounts it read-only in every graph cell. Planned debt: `gratifyWidgets.ts` and
`gratifyRangeMath.ts` are copies of gratify's example slider and range until
TKT-23 rebuilds them as this package's own parts.
