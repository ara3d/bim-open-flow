// The canvas editor: mounts the gratify graph view on a <canvas> and keeps it
// in sync with the store. Data flows one way: gestures -> CanvasIntents ->
// store dispatches (in canvasIntents.ts) -> store subscription -> "sync"
// intent rebuilding the canvas doc from the store.

import { mount, type Runtime } from "gratify";
import { applyCanvasTheme, defaultCanvasTheme, type CanvasThemeName } from "./canvasTheme.js";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "./canvasIntents.js";
import { canvasView } from "./canvasParts.js";
import { buildCanvasModel, type CanvasModel } from "./viewModel.js";

export interface CanvasEditor {
  /** Re-derives the canvas doc from the store (e.g. after the catalog loads). */
  refresh(): void;
  /** Switches the canvas theme (see canvasTheme.ts for the names). */
  setTheme(theme: CanvasThemeName): void;
  dispose(): void;
}

export function createCanvasEditor(
  canvas: HTMLCanvasElement,
  store: Store,
  getCatalog: () => ReadonlyMap<string, NodeDescriptor>,
  onError: (message: string) => void,
): CanvasEditor {
  applyCanvasTheme(defaultCanvasTheme);
  const model = (): CanvasModel => buildCanvasModel(store.getState(), getCatalog());
  const runtime: Runtime<CanvasModel, CanvasIntent> = mount(canvas, {
    init: model(),
    update: makeCanvasUpdate(store, onError),
    view: canvasView,
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
      runtime.dispatch({ kind: "sync", model: model() });
    });
  };
  const unsubscribe = store.subscribe(sync);

  return {
    refresh: sync,
    // Live swap: gratify retargets its tokens and cross-fades; the sync wakes
    // the runtime's frame loop so the fade actually runs. Pan/zoom untouched.
    setTheme: (theme) => {
      applyCanvasTheme(theme);
      sync();
    },
    dispose: () => {
      unsubscribe();
      runtime.stop();
    },
  };
}
