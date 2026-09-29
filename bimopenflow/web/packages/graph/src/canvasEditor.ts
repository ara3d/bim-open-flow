// The graph editor: mounts the gratify graph view on a <canvas> and keeps it
// in sync with the store. Data flows one way: gestures -> CanvasIntents ->
// store dispatches (in canvasIntents.ts) -> store subscription -> "sync"
// intent rebuilding the canvas doc from the store. Everything the mount owns
// lives in its CanvasInstance, so several editors can share a page.

import { mount, v, type Runtime } from "gratify";
import { applyCanvasTheme, defaultCanvasTheme, type CanvasThemeName } from "./canvasTheme.js";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type AnchorRef, type CanvasIntent } from "./canvasIntents.js";
import { canvasView } from "./canvasParts.js";
import { createCanvasInstance, pruneInstance, type SuggestionProvider } from "./instance.js";
import { islandKey } from "./slotShared.js";
import { buildCanvasModel, NOTE_KIND, type CanvasModel, type NodeBounds } from "./viewModel.js";
import { animateSelection } from "./selectionBorder.js";
import { installNodeContextMenu } from "./nodeContextMenu.js";
import { createPeekWiring, type PeekWiring } from "./peekWiring.js";
import { NO_PORT_RESULTS, type PortResultsView, type ReadPort } from "./portResults.js";

/** A point in CSS pixels relative to the page (client) or in canvas world units. */
export interface Point { readonly x: number; readonly y: number }

/** Gestures the canvas hands to the host's chrome (the studio's palette,
 *  TKT-96). Every hook is optional; a viewer and the demo pages pass none. */
export interface CanvasEditorSurfaces {
  /** A wire released over empty canvas: its source anchor, the drop point in
   *  world units, and the same point in client pixels for placing a popup. */
  onWireDropped?(from: AnchorRef, world: Point, client: Point): void;
  /** A right-click that hit no node. */
  onEmptyCanvas?(client: Point, world: Point): void;
  /** Row counts or the open peek changed (only with a readPort). */
  onResultsChange?(): void;
}

export interface GraphEditorOptions {
  readonly store: Store;
  readonly catalog: () => ReadonlyMap<string, NodeDescriptor>;
  /** A rejected gesture reports here instead of crashing the frame loop. */
  readonly onError: (message: string) => void;
  /** A viewer: pans, zooms, hovers, and selects, and never changes the document. Default false. */
  readonly readOnly?: boolean;
  /** The canvas theme at mount. The theme is page-wide (gratify's tokens are), so every mounted canvas follows a later setTheme. */
  readonly theme?: CanvasThemeName;
  /** The node whose upstream path is highlighted; defaults to the last selected node. */
  readonly getPreview?: () => string | null;
  /** Double-click on a node (TKT-81): show it without selecting it. */
  readonly onShowNode?: (nodeId: string) => void;
  /** Host reader for wire row counts and peeks (TKT-11); without it the canvas shows neither. */
  readonly readPort?: ReadPort;
  /** Live values for suggest-annotated parameters. */
  readonly suggestions?: SuggestionProvider;
  /** Gestures the canvas hands to the host's chrome; see CanvasEditorSurfaces. */
  readonly surfaces?: CanvasEditorSurfaces;
}

export interface FitOptions {
  /** The zoom fit never goes below. Default 0.1, the studio's; a notebook cell passes a higher floor
   *  so node text stays readable, and a graph that then does not fit overflows (see `overflow`). */
  readonly minZoom?: number;
  /** Space kept free on every side, in CSS pixels. Default: 24 at the sides and bottom and 48 at
   *  the top, where the studio's canvas carries its overlay buttons. */
  readonly margin?: number;
  /** Where a graph that does not fit at `minZoom` sits on that axis: "start" anchors its top-left
   *  corner (the studio's default), "center" crops both ends evenly. */
  readonly overflow?: "start" | "center";
}

/** A rectangle in world units. */
export interface Rect { readonly x: number; readonly y: number; readonly width: number; readonly height: number }

/** Pan (CSS pixels) and zoom, as gratify's runtime holds them. */
export interface Viewport { readonly zoom: number; readonly pan: Point }

/** The rectangle every node covers, or undefined for an empty graph. */
export function contentBounds(nodes: readonly NodeBounds[]): Rect | undefined {
  if (!nodes.length) return undefined;
  const x = Math.min(...nodes.map(n => n.x));
  const y = Math.min(...nodes.map(n => n.y));
  return {
    x, y,
    width: Math.max(...nodes.map(n => n.x + n.w)) - x,
    height: Math.max(...nodes.map(n => n.y + n.h)) - y,
  };
}

/** The viewport that frames `content` in a view of `view` CSS pixels: the largest zoom up to 1 that
 *  fits inside the margins, never below `minZoom`, centred on each axis where it fits. */
export function fitViewport(content: Rect, view: { readonly width: number; readonly height: number },
  options: FitOptions = {}): Viewport {
  const m = options.margin;
  const [top, right, bottom, left] = m === undefined ? [48, 24, 24, 24] : [m, m, m, m];
  const boxW = view.width - left - right;
  const boxH = view.height - top - bottom;
  const zoom = Math.max(options.minZoom ?? 0.1, Math.min(1, boxW / content.width, boxH / content.height));
  // Centred inside the margin box; a graph that overflows at the floor is centred too, or held at the box's start.
  const place = (start: number, box: number, size: number, origin: number): number =>
    (size * zoom <= box || options.overflow === "center" ? start + (box - size * zoom) / 2 : start) - origin * zoom;
  return { zoom, pan: {
    x: place(left, boxW, content.width, content.x),
    y: place(top, boxH, content.height, content.y),
  } };
}

export interface GraphEditor {
  /** Re-derives the canvas doc from the store (e.g. after the catalog loads). */
  refresh(): void;
  /** The row counts and open peek the canvas draws; empty without a readPort. */
  results(): PortResultsView;
  /** Frames every node with fitViewport (see FitOptions). */
  fit(options?: FitOptions): void;
  /** The rectangle the nodes cover in world units, as laid out now; undefined for an empty graph. */
  bounds(): Rect | undefined;
  focus(nodeId?: string): void;
  /** Re-reads the column options of every column selector (after an evaluation update). */
  refreshSuggestions(): void;
  /** Switches the canvas theme (see canvasTheme.ts for the names). Page-wide. */
  setTheme(theme: CanvasThemeName): void;
  dispose(): void;
}

export function createGraphEditor(canvas: HTMLCanvasElement, options: GraphEditorOptions): GraphEditor {
  const { store, onError } = options;
  const getCatalog = options.catalog;
  const getPreview = options.getPreview ?? (() => store.getState().selection.at(-1) ?? null);
  const onShowNode = options.onShowNode ?? (() => {});
  const readOnly = options.readOnly ?? false;
  const surfaces = options.surfaces ?? {};
  applyCanvasTheme(options.theme ?? defaultCanvasTheme, /* instant: */ true);
  const instance = createCanvasInstance({
    document: canvas.ownerDocument,
    readOnly,
    suggestionProvider: options.suggestions ?? null,
  });
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
    }, readOnly),
    view: (doc) => canvasView(doc, instance),
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
  instance.dispatch = (intent) => runtime.dispatch(intent);

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

  // A viewer has no delete and no palette: the context menu is not installed at all.
  const disposeContextMenu = readOnly ? () => {} : installNodeContextMenu(canvas, {
    hitNode: (x, y) => nodeAtWorld(canvasToWorld(x, y)),
    onDelete(nodeId) {
      try { store.dispatch({ type: "removeNode", id: nodeId }); }
      catch (error) { onError(error instanceof Error ? error.message : String(error)); }
    },
    onEmptyCanvas: (clientX, clientY, canvasX, canvasY) =>
      surfaces.onEmptyCanvas?.({ x: clientX, y: clientY }, canvasToWorld(canvasX, canvasY)),
  });

  // Double-click shows a node in the pane without selecting it (TKT-81).
  const onDoubleClick = (event: MouseEvent) => {
    const nodeId = nodeAtWorld(clientToWorld(event.clientX, event.clientY));
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
      pruneInstance(
        instance,
        new Set(next.nodes.flatMap((n) => n.params.map((p) => islandKey(n.id, p.name)))),
        new Set(next.nodes.filter((n) => n.kind === NOTE_KIND).map((n) => n.id)),
      );
      holdRequested = true;
      runtime.dispatch({ kind: "sync", model: next });
    });
  };
  const unsubscribe = store.subscribe(sync);

  if (options.readPort) {
    wiring = createPeekWiring(canvas, {
      store, getCatalog, readPort: options.readPort,
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
    fit(fitOptions = {}) {
      const content = contentBounds(model().nodes);
      if (!content) return;
      const { zoom, pan } = fitViewport(content, { width: canvas.clientWidth, height: canvas.clientHeight }, fitOptions);
      runtime.viewport = { zoom, pan: v(pan.x, pan.y) };
      sync();
    },
    bounds: () => contentBounds(model().nodes),
    refreshSuggestions: () => instance.columnSelects.refresh(),
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
      pruneInstance(instance, new Set());
      instance.dispatch = () => {};
      runtime.stop();
    },
  };
}
