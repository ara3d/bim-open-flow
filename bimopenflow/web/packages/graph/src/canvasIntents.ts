// Canvas intent vocabulary and the gratify update function. Every graph
// mutation is forwarded to the store (the single mutation path, P2); only
// transient presentation (a node mid-drag, the selected wire) lives in the
// canvas doc. Gratify-free so it is testable headless.

import type { PortType } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import { NOTE_KIND, type CanvasModel } from "./viewModel.js";
import { previewAfterEdit } from "./graphPreview";
import { slotControl } from "./canvasSlots.js";

export type AnchorDir = "in" | "out";

/** Anchor id for a port: "in:nodeId.port" / "out:nodeId.port". */
export function anchorId(dir: AnchorDir, nodeId: string, port: string): string {
  return `${dir}:${nodeId}.${port}`;
}

export interface AnchorRef {
  readonly dir: AnchorDir;
  readonly nodeId: string;
  readonly port: string;
  /** "nodeId.port" — the graph-document endpoint form. */
  readonly endpoint: string;
}

export function parseAnchorId(id: string): AnchorRef {
  const colon = id.indexOf(":");
  const dir = id.slice(0, colon) as AnchorDir;
  const endpoint = id.slice(colon + 1);
  const dot = endpoint.indexOf(".");
  return { dir, nodeId: endpoint.slice(0, dot), port: endpoint.slice(dot + 1), endpoint };
}

export function portTypesCompatible(a: PortType, b: PortType): boolean {
  return a === "Any" || b === "Any" || a === b;
}

/** A wire may connect an output to an input of a compatible type on another node. */
export function canConnect(
  from: { dir: AnchorDir; nodeId: string; type: PortType },
  to: { dir: AnchorDir; nodeId: string; type: PortType },
): boolean {
  return (
    from.dir !== to.dir &&
    from.nodeId !== to.nodeId &&
    portTypesCompatible(from.type, to.type)
  );
}

export type CanvasIntent =
  | { kind: "sync"; model: CanvasModel }
  | { kind: "move"; id: string; x: number; y: number } // transient, during drag
  | { kind: "moveEnd"; id: string } // commits the dragged position to the store
  | { kind: "connect"; a: string; b: string } // two anchor ids, either order
  /** A dragged wire released over empty canvas (no snap): `from` is the
   *  anchor it started at, (x, y) the world point it was dropped on. The
   *  canvas palette (TKT-96) answers it with a kind list filtered to ports
   *  that can take the wire. */
  | { kind: "wireDropped"; from: string; x: number; y: number }
  | { kind: "setParam"; nodeId: string; name: string; value: string } // inline control commit
  | { kind: "selectNode"; id: string }
  | { kind: "selectEdge"; id: string | null } // transient wire selection
  | { kind: "clearSelection" }
  | { kind: "deleteSelected" }
  | { kind: "openEditor"; nodeId: string; name: string } // opens the long-value editor for one row
  | { kind: "closeEditor" };

/** Gestures the canvas cannot resolve alone; the editor supplies the DOM
 *  surfaces that answer them. Every hook is optional so headless tests and
 *  the demo pages need none. */
export interface CanvasHooks {
  /** A wire dropped on empty canvas; `from` is the parsed source anchor. */
  onWireDropped?(from: AnchorRef, x: number, y: number): void;
}

/** Intents that change the document or open an editor on it; a read-only
 *  canvas drops them here, whatever part raised them. Selection, the
 *  transient wire selection, closing an editor, and sync still run. */
export const MUTATING_INTENTS: ReadonlySet<CanvasIntent["kind"]> = new Set<CanvasIntent["kind"]>([
  "move", "moveEnd", "connect", "setParam", "deleteSelected", "openEditor", "wireDropped",
]);

/**
 * The gratify update function for the canvas, bound to the store. Store
 * dispatches are wrapped: the reducer throws on invalid edits, and a rejected
 * user gesture must report, not crash the frame loop. With `readOnly`, every
 * MUTATING_INTENTS member returns the doc unchanged: this is the one place a
 * viewer's guarantee rests on, whether or not a part also hides the gesture.
 */
export function makeCanvasUpdate(
  store: Store,
  onError: (message: string) => void,
  getPreview: () => string | null = () => store.getState().selection.at(-1) ?? null,
  hooks: CanvasHooks = {},
  readOnly = false,
): (doc: CanvasModel, intent: CanvasIntent) => CanvasModel {
  const dispatch = (action: Parameters<Store["dispatch"]>[0]): void => {
    try {
      store.dispatch(action);
    } catch (e) {
      onError(e instanceof Error ? e.message : String(e));
    }
  };

  return (doc, intent) => {
    if (readOnly && MUTATING_INTENTS.has(intent.kind)) return doc;
    switch (intent.kind) {
      case "sync": {
        if (doc.openEditor === null) return intent.model;
        const { nodeId, name } = doc.openEditor;
        const node = intent.model.nodes.find((n) => n.id === nodeId);
        const param = node?.params.find((p) => p.name === name);
        // An evaluation update can change a parameter's kind, control, or
        // suggest source (a catalog reload, a descriptor change) and stop it
        // being long text. longTextSlot is the only row the editor is placed
        // under, so once the row is a different control, keeping the editor
        // open would anchor it under a row that no longer renders it. A
        // view.note's "text" editor covers the whole card instead of a row,
        // so it stays open for as long as the note itself does.
        const stillOpen = (node?.kind === NOTE_KIND && name === "text")
          || (param !== undefined && slotControl(param) === "longText");
        return stillOpen ? { ...intent.model, openEditor: doc.openEditor } : intent.model;
      }

      case "move":
        return {
          ...doc,
          nodes: doc.nodes.map((n) =>
            n.id === intent.id ? { ...n, x: intent.x, y: intent.y } : n),
        };

      case "moveEnd": {
        const node = doc.nodes.find((n) => n.id === intent.id);
        if (node) dispatch({ type: "setLayout", nodeId: node.id, layout: { x: node.x, y: node.y } });
        return doc;
      }

      case "connect": {
        const a = parseAnchorId(intent.a);
        const b = parseAnchorId(intent.b);
        const [from, to] = a.dir === "out" ? [a, b] : [b, a];
        dispatch({ type: "connect", from: from.endpoint, to: to.endpoint });
        dispatch({ type: "select", ids: [previewAfterEdit(store.getState().document,to.nodeId,getPreview())] });
        return doc;
      }

      case "wireDropped":
        hooks.onWireDropped?.(parseAnchorId(intent.from), intent.x, intent.y);
        return doc;

      case "setParam":
        dispatch({ type: "select", ids: [previewAfterEdit(store.getState().document,intent.nodeId,getPreview())] });
        dispatch({
          type: "setParam",
          nodeId: intent.nodeId,
          name: intent.name,
          value: intent.value,
        });
        return doc;

      case "selectNode":
        dispatch({ type: "select", ids: [intent.id] });
        return doc;

      case "selectEdge":
        return { ...doc, selectedEdgeId: intent.id };

      case "clearSelection":
        dispatch({ type: "clearSelection" });
        return { ...doc, selectedEdgeId: null };

      case "openEditor":
        return { ...doc, openEditor: { nodeId: intent.nodeId, name: intent.name } };

      case "closeEditor":
        return { ...doc, openEditor: null };

      case "deleteSelected": {
        if (doc.selectedEdgeId) {
          const edge = doc.edges.find((e) => e.id === doc.selectedEdgeId);
          if (edge) dispatch({ type: "disconnect", from: edge.from, to: edge.to });
          return { ...doc, selectedEdgeId: null };
        }
        for (const id of store.getState().selection)
          dispatch({ type: "removeNode", id });
        return doc;
      }
    }
  };
}
