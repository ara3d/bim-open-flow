// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";

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
export const PORT_SPACING = 20;
export const NODE_HEADER = 36;

/** Node height grows with its densest port side. */
export function nodeHeight(inputCount: number, outputCount: number): number {
  return NODE_HEADER + Math.max(inputCount, outputCount, 1) * PORT_SPACING;
}

/** Deterministic grid position for the n-th node without saved layout. */
export function defaultPosition(index: number): { x: number; y: number } {
  const cols = 4;
  return { x: 80 + (index % cols) * (NODE_WIDTH + 60), y: 80 + Math.floor(index / cols) * 130 };
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
    const layout = state.document.layout[n.id];
    const pos = layout ?? defaultPosition(unplaced++);
    return {
      id: n.id,
      kind: n.kind,
      x: pos.x,
      y: pos.y,
      w: layout?.w ?? NODE_WIDTH,
      h: layout?.h ?? nodeHeight(inputs.length, outputs.length),
      inputs: inputs.map((p) => ({ name: p.name, type: p.type })),
      outputs: outputs.map((p) => ({ name: p.name, type: p.type })),
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
