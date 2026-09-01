// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { inlineParams, placeSlots, type CanvasParam } from "./canvasSlots.js";

export interface CanvasPort {
  readonly name: string;
  readonly type: PortType;
}

export interface CanvasNode {
  readonly id: string;
  readonly kind: string;
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
  readonly inputs: readonly CanvasPort[];
  readonly outputs: readonly CanvasPort[];
  /** Inline-editable params (catalog order, document values applied). */
  readonly params: readonly CanvasParam[];
  readonly status?: NodeStatus;
  readonly selected: boolean;
}

export interface CanvasEdge {
  readonly id: string; // "from->to", stable across rebuilds
  readonly from: string; // "nodeId.port"
  readonly to: string;
}

export interface CanvasModel {
  readonly nodes: readonly CanvasNode[];
  readonly edges: readonly CanvasEdge[];
  readonly selectedEdgeId: string | null;
}

export const NODE_WIDTH = 184;
/** Nodes with inline param slots get extra width so field values stay legible. */
export const WIDE_NODE_WIDTH = 240;
export const PORT_SPACING = 20;
export const NODE_HEADER = 36;

export function nodeWidth(params: readonly CanvasParam[]): number {
  return params.length > 0 ? WIDE_NODE_WIDTH : NODE_WIDTH;
}

/** Node height grows with its densest port side, then with its param slots —
 *  each slot contributes the height its control kind needs. */
export function nodeHeight(
  inputCount: number,
  outputCount: number,
  params: readonly CanvasParam[] = [],
): number {
  const portsBottom = NODE_HEADER + Math.max(inputCount, outputCount, 1) * PORT_SPACING;
  return placeSlots(params, portsBottom).bottom;
}

/** Deterministic grid position for the n-th node without saved layout. */
export function defaultPosition(index: number): { x: number; y: number } {
  const cols = 4;
  return { x: 80 + (index % cols) * (NODE_WIDTH + 60), y: 80 + Math.floor(index / cols) * 130 };
}

export interface NodeBounds {
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
}

/**
 * The first free spot for a new node of the given size: scans a coarse grid
 * left-to-right, top-to-bottom and returns the first position where the node
 * (plus a margin) overlaps nothing. Size-aware, so tall inline-param nodes
 * never land on top of their neighbors.
 */
export function freePosition(
  existing: readonly NodeBounds[],
  w: number,
  h: number,
): { x: number; y: number } {
  const MARGIN = 24;
  const STEP = 40;
  const X0 = 80;
  const Y0 = 80;
  const COLS = 26; // keep the layout roughly viewport-shaped before wrapping
  const collides = (x: number, y: number) =>
    existing.some(
      (r) =>
        x < r.x + r.w + MARGIN &&
        r.x < x + w + MARGIN &&
        y < r.y + r.h + MARGIN &&
        r.y < y + h + MARGIN,
    );
  for (let y = Y0; y < Y0 + 400 * STEP; y += STEP)
    for (let x = X0; x <= X0 + COLS * STEP; x += STEP)
      if (!collides(x, y)) return { x, y };
  return defaultPosition(existing.length);
}

export function edgeId(from: string, to: string): string {
  return `${from}->${to}`;
}

/** Builds the drawable model; catalog gaps degrade to portless nodes. */
export function buildCanvasModel(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
): CanvasModel {
  const selected = new Set(state.selection);
  let unplaced = 0;
  const nodes = state.document.structure.nodes.map((n) => {
    const desc = catalog.get(n.kind);
    const inputs = desc?.inputs ?? [];
    const outputs = desc?.outputs ?? [];
    const params = inlineParams(desc?.params ?? [], state.document.values[n.id] ?? {});
    const layout = state.document.layout[n.id];
    const pos = layout ?? defaultPosition(unplaced++);
    return {
      id: n.id,
      kind: n.kind,
      x: pos.x,
      y: pos.y,
      w: layout?.w ?? nodeWidth(params),
      h: layout?.h ?? nodeHeight(inputs.length, outputs.length, params),
      inputs: inputs.map((p) => ({ name: p.name, type: p.type })),
      outputs: outputs.map((p) => ({ name: p.name, type: p.type })),
      params,
      status: state.evalState[n.id]?.status,
      selected: selected.has(n.id),
    };
  });
  const edges = state.document.structure.edges.map((e) => ({
    id: edgeId(e.from, e.to),
    from: e.from,
    to: e.to,
  }));
  return { nodes, edges, selectedEdgeId: null };
}
