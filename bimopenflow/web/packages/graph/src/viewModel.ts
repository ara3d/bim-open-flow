// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { inlineParams, type CanvasParam } from "./canvasSlots.js";
import { NODE_WIDTH, nodeSize, NOTE_KIND } from "./nodeSize.js";
import { upstreamIds } from "./graphPreview";
import { nodeBadge, type NodeBadge } from "./nodeBadge.js";
import { NO_PORT_RESULTS, type PortPeekView, type PortResultsView, type WireRows } from "./portResults.js";

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
  /** Status badge text, and the upstream node responsible when one exists
   *  (TKT-10). Undefined alongside `status` before the first eval update. */
  readonly badge?: NodeBadge;
  /** The kind's catalog description (TKT-98): the card draws it so a reader
   *  learns what the node does without the sidebar. Absent for a note or an
   *  unknown kind. */
  readonly description?: string;
  readonly selected: boolean;
  readonly contributing?: boolean;
  /** The `text` param's value, only for a `view.note` node (TKT-82): drawn as
   *  a sticky note instead of the normal ports/params card, so its text does
   *  not also travel through CanvasParam. */
  readonly noteText?: string;
}

export interface CanvasEdge {
  readonly id: string; // "from->to", stable across rebuilds
  readonly from: string; // "nodeId.port"
  readonly to: string;
  readonly contributing?: boolean;
  /** Row count of the table or relation leaving `from`; absent when uncounted. */
  readonly rows?: WireRows;
}

/** The parameter whose long-value editor is open; at most one. */
export interface OpenEditor {
  readonly nodeId: string;
  readonly name: string;
}

export interface CanvasModel {
  readonly nodes: readonly CanvasNode[];
  readonly edges: readonly CanvasEdge[];
  readonly selectedEdgeId: string | null;
  readonly openEditor: OpenEditor | null;
  /** The one open peek card, if any port is hovered or pinned. */
  readonly peek?: PortPeekView;
}

export {
  MAX_CONTENT_WIDTH, NODE_HEADER, NODE_WIDTH, nodeHeight, nodeSize, nodeWidth, NOTE_KIND, NOTE_LINE_H,
  NOTE_MAX_LINES, NOTE_PAD, NOTE_WIDTH, noteHeight, PORT_SPACING, WIDE_NODE_WIDTH,
} from "./nodeSize.js";

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
  preview: string | null = state.selection.at(-1) ?? null,
  results: PortResultsView = NO_PORT_RESULTS,
): CanvasModel {
  const selected = new Set(state.selection);
  const contributing = upstreamIds(state.document,preview);
  let unplaced = 0;
  const nodes = state.document.structure.nodes.map((n) => {
    const desc = catalog.get(n.kind);
    const inputs = desc?.inputs ?? [];
    const outputs = desc?.outputs ?? [];
    const params = inlineParams(desc?.params ?? [], state.document.values[n.id] ?? {});
    const layout = state.document.layout[n.id];
    const pos = layout ?? defaultPosition(unplaced++);
    if (n.kind === NOTE_KIND) {
      const noteText = state.document.values[n.id]?.["text"]
        ?? desc?.params.find((p) => p.name === "text")?.default
        ?? "";
      const note = { id: n.id, kind: n.kind, inputs: [], outputs: [], params: [], noteText };
      return {
        x: pos.x,
        y: pos.y,
        ...nodeSize(note, layout ?? {}),
        ...note,
        selected: selected.has(n.id),
        contributing: contributing.has(n.id),
      };
    }
    const card = {
      id: n.id,
      kind: n.kind,
      inputs: inputs.map((p) => ({ name: p.name, type: p.type })),
      outputs: outputs.map((p) => ({ name: p.name, type: p.type })),
      params,
    };
    return {
      x: pos.x,
      y: pos.y,
      ...nodeSize(card, layout ?? {}),
      ...card,
      status: state.evalState[n.id]?.status,
      badge: nodeBadge({ edges: state.document.structure.edges, evalState: state.evalState }, n.id),
      ...(desc?.description ? { description: desc.description } : {}),
      selected: selected.has(n.id),
      contributing: contributing.has(n.id),
    };
  });
  const edges = state.document.structure.edges.map((e) => {
    const rows = results.counts.get(e.from);
    return {
      id: edgeId(e.from, e.to),
      from: e.from,
      to: e.to,
      contributing: contributing.has(e.from.split(".")[0]!) && contributing.has(e.to.split(".")[0]!),
      ...(rows ? { rows: state.dirty ? { rows: rows.rows, current: false } : rows } : {}),
    };
  });
  return {
    nodes,
    edges,
    selectedEdgeId: null,
    openEditor: null,
    ...(results.peek ? { peek: results.peek } : {}),
  };
}
