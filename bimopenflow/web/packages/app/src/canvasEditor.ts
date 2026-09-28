// The canvas editor: mounts the gratify graph view on a <canvas> and keeps it
// in sync with the store. Data flows one way: gestures -> CanvasIntents ->
// store dispatches (in canvasIntents.ts) -> store subscription -> "sync"
// intent rebuilding the canvas doc from the store.

import { mount, v, type Runtime } from "gratify";
import { applyCanvasTheme, defaultCanvasTheme, type CanvasThemeName } from "./canvasTheme.js";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type AnchorRef, type CanvasIntent } from "./canvasIntents.js";
import { canvasView } from "./canvasParts.js";
import { disposeSlots, pruneSlots } from "./slotRegistry.js";
import { islandKey, setInlineControlDispatch } from "./slotShared.js";
import { buildCanvasModel, type CanvasModel } from "./viewModel.js";
import { animateSelection } from "./selectionBorder";
import { installNodeContextMenu } from "./nodeContextMenu";
import { createPeekWiring, type PeekWiring } from "./peekWiring.js";
import { NO_PORT_RESULTS, type PortResultsView, type ReadPort } from "./portResults.js";

/** A point in CSS pixels relative to the page (client) or in canvas world units. */
export interface Point { readonly x: number; readonly y: number }

/** Gestures the canvas hands to app-side chrome (the palette, TKT-96). Every
 *  hook is optional; the demo pages pass none. */
export interface CanvasEditorSurfaces {
  /** A wire released over empty canvas: its source anchor, the drop point in
   *  world units, and the same point in client pixels for placing a popup. */
  onWireDropped?(from: AnchorRef, world: Point, client: Point): void;
  /** A right-click that hit no node. */
  onEmptyCanvas?(client: Point, world: Point): void;
  /** Row counts or the open peek changed (only with a readPort). */
  onResultsChange?(): void;
}

export interface CanvasEditor {
  /** Re-derives the canvas doc from the store (e.g. after the catalog loads). */
  refresh(): void;
  /** The row counts and open peek the canvas draws; empty without a readPort. */
  results(): PortResultsView;
  fit(): void;
  focus(nodeId?: string): void;
  /** Switches the canvas theme (see canvasTheme.ts for the names). */
  setTheme(theme: CanvasThemeName): void;
  dispose(): void;
}

export function createCanvasEditor(
  canvas: HTMLCanvasElement,
  store: Store,
  getCatalog: () => ReadonlyMap<string, NodeDescriptor>,
  onError: (message: string) => void,
  initialTheme: CanvasThemeName = defaultCanvasTheme,
  getPreview: () => string | null = () => store.getState().selection.at(-1) ?? null,
  // TKT-81: double-clicking a node is the discoverable way to look at
  // something other than the flow's answer, without leaving it selected.
  onShowNode: (nodeId: string) => void = () => {},
  // TKT-11: when given, hovering an output socket or wire peeks its rows and
  // wires show row counts. Absent, the editor behaves as before.
  readPort?: ReadPort,
  surfaces: CanvasEditorSurfaces = {},
): CanvasEditor {
  applyCanvasTheme(initialTheme, /* instant: */ true);
  let wiring: PeekWiring | null = null; // created once the runtime exists
  const model = (): CanvasModel =>
    buildCanvasModel(store.getState(), getCatalog(), getPreview(), wiring?.view());
  // The runtime's rest detector can doze off mid entrance-animation right
  // after a doc swap (boot, flow open), freezing the canvas on ghost-faint
  // nodes until the next interaction. `ambient` holds the loop awake briefly
  // after every sync so entrances and theme fades always run to completion.
  // The same clause also keeps it awake while a node is selected: that is
  // exactly when the selection-border pulse and the TKT-24 wire-flow
  // animation (upstreamEdges in canvasParts.ts) are both running, and both
  // fall back to a static look under prefers-reduced-motion.
  let awakeUntil = 0;
  let holdRequested = true; // cover the very first frames after mount
  const runtime: Runtime<CanvasModel, CanvasIntent> = mount(canvas, {
    init: model(),
    update: makeCanvasUpdate(store, onError, getPreview, {
      onWireDropped: (from, x, y) => surfaces.onWireDropped?.(from, { x, y }, toClient({ x, y })),
    }),
    view: canvasView,
    ambient: (_doc, time) => {
      if (holdRequested) {
        holdRequested = false;
        awakeUntil = time + 1.5;
      }
      return time < awakeUntil || (animateSelection() && _doc.nodes.some(n => n.selected));
    },
  });
  // Island inputs (inline Text/FilePath/DateTime/number controls) live in the
  // DOM, outside gratify's intent flow; their commits come back through here.
  setInlineControlDispatch((intent) => runtime.dispatch(intent));

  // Coordinate frames: client (page CSS pixels), canvas (CSS pixels from the
  // canvas's top-left), world (the graph's own units, under pan and zoom).
  const canvasToWorld = (x: number, y: number): Point => {
    const { zoom, pan } = runtime.viewport;
    return { x: (x - pan.x) / zoom, y: (y - pan.y) / zoom };
  };
  const clientToWorld = (clientX: number, clientY: number): Point => {
    const bounds = canvas.getBoundingClientRect();
    return canvasToWorld(clientX - bounds.left, clientY - bounds.top);
  };
  const toClient = (world: Point): Point => {
    const bounds = canvas.getBoundingClientRect();
    const { zoom, pan } = runtime.viewport;
    return { x: bounds.left + pan.x + world.x * zoom, y: bounds.top + pan.y + world.y * zoom };
  };
  const nodeAtWorld = (p: Point): string | null =>
    [...runtime.doc.nodes].reverse().find((n) =>
      p.x >= n.x && p.x <= n.x + n.w && p.y >= n.y && p.y <= n.y + n.h)?.id ?? null;

  const disposeContextMenu = installNodeContextMenu(canvas, {
    hitNode: (x, y) => nodeAtWorld(canvasToWorld(x, y)),
    onDelete(nodeId) {
      try { store.dispatch({ type: "removeNode", id: nodeId }); }
      catch (error) { onError(error instanceof Error ? error.message : String(error)); }
    },
    onEmptyCanvas: (clientX, clientY, canvasX, canvasY) =>
      surfaces.onEmptyCanvas?.({ x: clientX, y: clientY }, canvasToWorld(canvasX, canvasY)),
  });

  // Double-click shows a node in the pane without selecting it (TKT-81).
  const hitNode = (clientX: number, clientY: number): string | null =>
    nodeAtWorld(clientToWorld(clientX, clientY));
  const onDoubleClick = (event: MouseEvent) => {
    const nodeId = hitNode(event.clientX, event.clientY);
    if (nodeId !== null) onShowNode(nodeId);
  };
  canvas.addEventListener("dblclick", onDoubleClick);

  // Store dispatches can originate inside a gratify update (a gesture intent);
  // syncing re-entrantly would be overwritten by the outer update's return
  // value, so the sync is deferred one microtask.
  // TODO: diff instead of full rebuild if large graphs make this hot.
  let queued = false;
  const sync = () => {
    if (queued) return;
    queued = true;
    queueMicrotask(() => {
      queued = false;
      const next = model();
      pruneSlots(new Set(
        next.nodes.flatMap((n) => n.params.map((p) => islandKey(n.id, p.name))),
      ));
      holdRequested = true;
      runtime.dispatch({ kind: "sync", model: next });
    });
  };
  const unsubscribe = store.subscribe(sync);
  if (readPort) {
    wiring = createPeekWiring(canvas, {
      store, getCatalog, readPort,
      getDoc: () => runtime.doc,
      getViewport: () => runtime.viewport,
      onChange: () => { sync(); surfaces.onResultsChange?.(); },
    });
  }

  return {
    refresh: sync,
    results: () => wiring?.view() ?? NO_PORT_RESULTS,
    focus(nodeId) {
      const node = model().nodes.find(n => n.id === nodeId) ?? model().nodes[0];
      if (!node) return;
      runtime.viewport = { zoom: 1, pan: v(canvas.clientWidth / 2 - node.x - node.w / 2,
        Math.max(50, canvas.clientHeight / 3 - node.h / 2) - node.y) };
      sync();
    },
    fit() {
      const nodes = model().nodes;
      if (!nodes.length) return;
      const left = Math.min(...nodes.map(n => n.x));
      const top = Math.min(...nodes.map(n => n.y));
      const width = Math.max(...nodes.map(n => n.x + n.w)) - left;
      const height = Math.max(...nodes.map(n => n.y + n.h)) - top;
      const zoom = Math.max(0.1, Math.min(1, (canvas.clientWidth - 48) / width, (canvas.clientHeight - 72) / height));
      runtime.viewport = { zoom, pan: v((canvas.clientWidth - width * zoom) / 2 - left * zoom,
        (canvas.clientHeight - height * zoom) / 2 - top * zoom + 12) };
      sync();
    },
    // Live swap: gratify retargets its tokens and cross-fades; the sync wakes
    // the runtime's frame loop so the fade actually runs. Pan/zoom untouched.
    setTheme: (theme) => {
      applyCanvasTheme(theme);
      sync();
    },
    dispose: () => {
      unsubscribe();
      wiring?.dispose();
      disposeContextMenu();
      canvas.removeEventListener("dblclick", onDoubleClick);
      disposeSlots();
      runtime.stop();
    },
  };
}
