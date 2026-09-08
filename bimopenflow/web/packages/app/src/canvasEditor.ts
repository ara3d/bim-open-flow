// The canvas editor: mounts the gratify graph view on a <canvas> and keeps it
// in sync with the store. Data flows one way: gestures -> CanvasIntents ->
// store dispatches (in canvasIntents.ts) -> store subscription -> "sync"
// intent rebuilding the canvas doc from the store.

import { mount, v, type Runtime } from "gratify";
import { applyCanvasTheme, defaultCanvasTheme, type CanvasThemeName } from "./canvasTheme.js";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "./canvasIntents.js";
import { canvasView } from "./canvasParts.js";
import {
  disposeInlineControls,
  islandKey,
  pruneInlineControls,
  setInlineControlDispatch,
} from "./canvasControls.js";
import { buildCanvasModel, type CanvasModel } from "./viewModel.js";
import { animateSelection } from "./selectionBorder";
import { installNodeContextMenu } from "./nodeContextMenu";

export interface CanvasEditor {
  /** Re-derives the canvas doc from the store (e.g. after the catalog loads). */
  refresh(): void;
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
): CanvasEditor {
  applyCanvasTheme(initialTheme, /* instant: */ true);
  const model = (): CanvasModel => buildCanvasModel(store.getState(), getCatalog(), getPreview());
  // The runtime's rest detector can doze off mid entrance-animation right
  // after a doc swap (boot, flow open), freezing the canvas on ghost-faint
  // nodes until the next interaction. `ambient` holds the loop awake briefly
  // after every sync so entrances and theme fades always run to completion.
  let awakeUntil = 0;
  let holdRequested = true; // cover the very first frames after mount
  const runtime: Runtime<CanvasModel, CanvasIntent> = mount(canvas, {
    init: model(),
    update: makeCanvasUpdate(store, onError, getPreview),
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
  const disposeContextMenu = installNodeContextMenu(canvas, {
    hitNode(x, y) {
      const { zoom, pan } = runtime.viewport;
      const px = (x - pan.x) / zoom;
      const py = (y - pan.y) / zoom;
      return [...runtime.doc.nodes].reverse().find(n =>
        px >= n.x && px <= n.x + n.w && py >= n.y && py <= n.y + n.h)?.id ?? null;
    },
    onDelete(nodeId) {
      try { store.dispatch({ type: "removeNode", id: nodeId }); }
      catch (error) { onError(error instanceof Error ? error.message : String(error)); }
    },
  });

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
      pruneInlineControls(new Set(
        next.nodes.flatMap((n) => n.params.map((p) => islandKey(n.id, p.name))),
      ));
      holdRequested = true;
      runtime.dispatch({ kind: "sync", model: next });
    });
  };
  const unsubscribe = store.subscribe(sync);

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
    // Live swap: gratify retargets its tokens and cross-fades; the sync wakes
    // the runtime's frame loop so the fade actually runs. Pan/zoom untouched.
    setTheme: (theme) => {
      applyCanvasTheme(theme);
      sync();
    },
    dispose: () => {
      unsubscribe();
      disposeContextMenu();
      disposeInlineControls();
      runtime.stop();
    },
  };
}
