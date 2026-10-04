// Connects wire peeking to a canvas: the port-results controller, the
// evaluation watcher, and the hover listener, behind one create/dispose pair
// so canvasEditor.ts stays small and the wiring is testable without gratify.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import type { CanvasModel } from "./viewModel.js";
import { grabRadius, peekTargetAt, SOCKET_GRAB_RADIUS } from "./portGeometry.js";
import { installPortHover } from "./portHover.js";
import { createPortResults, watchEvaluations, type PortResultsView, type ReadPort } from "./portResults.js";

export interface PeekWiringDeps {
  readonly store: Store;
  readonly getCatalog: () => ReadonlyMap<string, NodeDescriptor>;
  readonly readPort: ReadPort;
  /** The canvas doc and viewport the hit-test reads, as the runtime holds them. */
  readonly getDoc: () => CanvasModel;
  readonly getViewport: () => { zoom: number; pan: { x: number; y: number } };
  /** Called after every controller change; the editor re-syncs the canvas. */
  readonly onChange: () => void;
}

export interface PeekWiring {
  /** The snapshot buildCanvasModel takes as its `results` argument. */
  view(): PortResultsView;
  dispose(): void;
}

/** Creates the controller, watches evaluations, and installs the hover
 *  listener on `canvas`, converting CSS pixels to world coordinates through
 *  the viewport (as canvasEditor's context-menu hit test does). */
export function createPeekWiring(canvas: HTMLCanvasElement, deps: PeekWiringDeps): PeekWiring {
  const { store } = deps;
  const results = createPortResults(deps.readPort, deps.onChange);
  const stopWatching = watchEvaluations(store, deps.getCatalog, results);
  const stopHover = installPortHover(canvas, {
    targetAt(x, y) {
      const { zoom, pan } = deps.getViewport();
      return peekTargetAt(deps.getDoc(), (x - pan.x) / zoom, (y - pan.y) / zoom, grabRadius(SOCKET_GRAB_RADIUS, zoom));
    },
    hover: (endpoint) => results.hover(endpoint, store.getState()),
    pin: (endpoint) => results.pin(endpoint, store.getState()),
  });
  return {
    view: () => results.view(),
    dispose() {
      stopHover();
      stopWatching();
      results.dispose();
    },
  };
}
