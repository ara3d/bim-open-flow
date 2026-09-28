// The graph editor: mounts the gratify graph view on a <canvas> and keeps it
// in sync with the store. Data flows one way: gestures -> CanvasIntents ->
// store dispatches (in canvasIntents.ts) -> store subscription -> "sync"
// intent rebuilding the canvas doc from the store. Everything the mount owns
// lives in its CanvasInstance, so several editors can share a page.

import { mount, v, type Runtime } from "gratify";
import { applyCanvasTheme, defaultCanvasTheme, type CanvasThemeName } from "./canvasTheme.js";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasHooks, type CanvasIntent } from "./canvasIntents.js";
import { canvasView } from "./canvasParts.js";
import { createCanvasInstance, pruneInstance, type SuggestionProvider } from "./instance.js";
import { islandKey } from "./slotShared.js";
import { buildCanvasModel, NOTE_KIND, type CanvasModel } from "./viewModel.js";
import { animateSelection } from "./selectionBorder.js";
import { installNodeContextMenu } from "./nodeContextMenu.js";
import { createPeekWiring, type PeekWiring } from "./peekWiring.js";
import type { ReadPort } from "./portResults.js";

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
  /** Gestures the canvas cannot resolve alone (a wire dropped on empty canvas). */
  readonly hooks?: CanvasHooks;
}

export interface GraphEditor {
  /** Re-derives the canvas doc from the store (e.g. after the catalog loads). */
  refresh(): void;
  fit(): void;
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
    update: makeCanvasUpdate(store, onError, getPreview, options.hooks),
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

  // Both hit tests work in canvas (unzoomed, unpanned) space.
  const hitNodeAt = (px: number, py: number): string | null =>
    [...runtime.doc.nodes].reverse().find(n =>
      px >= n.x && px <= n.x + n.w && py >= n.y && py <= n.y + n.h)?.id ?? null;
  const hitNodeAtCanvas = (x: number, y: number): string | null => {
    const { zoom, pan } = runtime.viewport;
    return hitNodeAt((x - pan.x) / zoom, (y - pan.y) / zoom);
  };

  // A viewer has no delete: the context menu is not installed at all.
  const disposeContextMenu = readOnly ? () => {} : installNodeContextMenu(canvas, {
    hitNode: hitNodeAtCanvas,
    onDelete(nodeId) {
      try { store.dispatch({ type: "removeNode", id: nodeId }); }
      catch (error) { onError(error instanceof Error ? error.message : String(error)); }
    },
  });

  // Double-click shows a node in the pane without selecting it (TKT-81).
  const onDoubleClick = (event: MouseEvent) => {
    const bounds = canvas.getBoundingClientRect();
    const nodeId = hitNodeAtCanvas(event.clientX - bounds.left, event.clientY - bounds.top);
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
      onChange: sync,
    });
  }

  return {
    refresh: sync,
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
