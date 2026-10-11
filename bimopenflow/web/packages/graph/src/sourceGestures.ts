// The editor's source gestures (TKT-138), kept out of canvasEditor.ts: the chip
// hover, a file dropped on the canvas, and the context-menu commands (rail and
// chip toggles, unlink, Share as source with its relink offer). Each gesture
// builds a plan (sourcePlans.ts) and dispatches it as one `batch`, so one undo
// step takes it back.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Action, Store } from "@bimopenflow/state";
import { chipAt, rowAt } from "./chipGeometry.js";
import { installChipHover } from "./chipHover.js";
import { installFileDrop } from "./fileDrop.js";
import type { MenuItem } from "./nodeContextMenu.js";
import { showRelinkOffer } from "./relinkOffer.js";
import { sourceMenuItems } from "./sourceMenu.js";
import { dropSourcePlan, promotePlan, relinkActions, type SourcePlan } from "./sourcePlans.js";
import { canInlineInput, canMakeInput, inlineInputPlan, inputOfParam, makeInputPlan, PATH_PARAM, type InputPlan } from "./inputPlans.js";
import { isSourceDescriptor, type SourceView } from "./sources.js";
import type { CanvasModel } from "./viewModel.js";

interface Point { readonly x: number; readonly y: number }

export interface SourceGestureDeps {
  readonly store: Store;
  readonly getCatalog: () => ReadonlyMap<string, NodeDescriptor>;
  readonly onError: (message: string) => void;
  /** The model as drawn, for hit tests. */
  readonly getDoc: () => CanvasModel;
  /** Canvas-relative CSS pixels to world units. */
  readonly canvasToWorld: (x: number, y: number) => Point;
  /** Client pixels to world units. */
  readonly clientToWorld: (x: number, y: number) => Point;
  readonly getView: () => SourceView;
  readonly setView: (view: SourceView) => void;
  /** The hovered chip's source changed (null: none). */
  readonly onLit: (sourceId: string | null) => void;
  /** A viewer drops files and shares nothing. */
  readonly readOnly: boolean;
}

export interface SourceGestures {
  /** The extra context-menu commands for the node under the pointer. */
  menuItems(nodeId: string, canvasX: number, canvasY: number, clientX: number, clientY: number): MenuItem[];
  dispose(): void;
}

export function installSourceGestures(canvas: HTMLCanvasElement, deps: SourceGestureDeps): SourceGestures {
  const { store, onError } = deps;
  const doc = canvas.ownerDocument;
  const dispatch = (actions: readonly Action[]): void => {
    try { store.dispatch({ type: "batch", actions }); }
    catch (error) { onError(error instanceof Error ? error.message : String(error)); }
  };
  /** Dispatches a plan, or reports its error; returns the plan when it ran. */
  const run = (plan: SourcePlan): Extract<SourcePlan, { actions: Action[] }> | null => {
    if ("error" in plan) { onError(plan.error); return null; }
    dispatch(plan.actions);
    return plan;
  };

  const disposeHover = installChipHover(canvas, {
    sourceAt(x, y) {
      const world = deps.canvasToWorld(x, y);
      return chipAt(deps.getDoc(), world.x, world.y)?.sourceId ?? null;
    },
    onHover: deps.onLit,
  });
  const disposeDrop = deps.readOnly ? () => {} : installFileDrop(canvas, {
    onFile(path, clientX, clientY) {
      run(dropSourcePlan(store.getState(), deps.getCatalog(), path, deps.clientToWorld(clientX, clientY)));
    },
  });

  /** Dispatches a graph-input plan, or reports its error. */
  const runInput = (plan: InputPlan): void => {
    if ("error" in plan) onError(plan.error);
    else dispatch(plan.actions);
  };

  function promote(nodeId: string, param: string, client: Point): void {
    const plan = run(promotePlan(store.getState(), deps.getCatalog(), nodeId, param));
    if (!plan || plan.relink.length === 0) return;
    showRelinkOffer({
      document: doc, at: client, source: plan.source.id, targets: plan.relink,
      onAccept: () => dispatch(relinkActions(plan.source, plan.relink)),
    });
  }

  return {
    menuItems: (nodeId, canvasX, canvasY, clientX, clientY) => {
      const world = deps.canvasToWorld(canvasX, canvasY);
      return sourceMenuItems({
        view: deps.getView,
        setView: deps.setView,
        isSource: (id) => isSourceDescriptor(deps.getCatalog().get(
          store.getState().document.structure.nodes.find((n) => n.id === id)?.kind ?? "")),
        chipAt: (x, y) => chipAt(deps.getDoc(), x, y),
        rowAt: (x, y) => rowAt(deps.getDoc(), x, y),
        promote: (id, param) => promote(id, param, { x: clientX, y: clientY }),
        unlink(hit) {
          const edge = store.getState().document.structure.edges.find((e) => e.to === `${hit.nodeId}.${hit.param}`);
          if (edge) store.dispatch({ type: "disconnect", from: edge.from, to: edge.to });
        },
        inputs: {
          canMake: (id) => canMakeInput(store.getState(), deps.getCatalog(), id),
          make: (id) => runInput(makeInputPlan(store.getState(), deps.getCatalog(), id)),
          inlineParam(id, row) {
            const state = store.getState();
            if (row?.nodeId === id && inputOfParam(state, id, row.param.name) !== null) return row.param.name;
            return canInlineInput(state, deps.getCatalog(), id) ? PATH_PARAM : null;
          },
          inline: (id, param) => runInput(inlineInputPlan(store.getState(), id, param)),
        },
      }, nodeId, world.x, world.y);
    },
    dispose() {
      disposeHover();
      disposeDrop();
    },
  };
}
