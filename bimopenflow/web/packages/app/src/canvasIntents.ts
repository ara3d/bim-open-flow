// Canvas intent vocabulary and the gratify update function. Every graph
// mutation is forwarded to the store (the single mutation path, P2); only
// transient presentation (a node mid-drag, the selected wire) lives in the
// canvas doc. Gratify-free so it is testable headless.

import type { PortType } from "@bimopenflow/contracts";
import type { Store } from "@bimopenflow/state";
import type { CanvasModel } from "./viewModel.js";

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
  | { kind: "setParam"; nodeId: string; name: string; value: string } // inline control commit
  | { kind: "selectNode"; id: string }
  | { kind: "selectEdge"; id: string | null } // transient wire selection
  | { kind: "clearSelection" }
  | { kind: "deleteSelected" };

/**
 * The gratify update function for the canvas, bound to the store. Store
 * dispatches are wrapped: the reducer throws on invalid edits, and a rejected
 * user gesture must report, not crash the frame loop.
 */
export function makeCanvasUpdate(
  store: Store,
  onError: (message: string) => void,
): (doc: CanvasModel, intent: CanvasIntent) => CanvasModel {
  const dispatch = (action: Parameters<Store["dispatch"]>[0]): void => {
    try {
      store.dispatch(action);
    } catch (e) {
      onError(e instanceof Error ? e.message : String(e));
    }
  };

  return (doc, intent) => {
    switch (intent.kind) {
      case "sync":
        return intent.model;

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
        return doc;
      }

      case "setParam":
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
