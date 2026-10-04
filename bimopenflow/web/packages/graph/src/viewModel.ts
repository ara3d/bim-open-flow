// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { inlineParams, type CanvasParam } from "./canvasSlots.js";
import { nodeSize, NOTE_KIND, type Size } from "./nodeSize.js";
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
  MAX_CONTENT_WIDTH, NODE_HEADER, NODE_WIDTH, nodeHeight, nodeSize, NOTE_KIND, NOTE_LINE_H,
  NOTE_MAX_LINES, NOTE_PAD, NOTE_WIDTH, noteHeight, PORT_SPACING, WIDE_NODE_WIDTH,
} from "./nodeSize.js";

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
  // The grid is full: below everything.
  return { x: X0, y: Math.max(Y0, ...existing.map((r) => r.y + r.h + MARGIN)) };
}

export function edgeId(from: string, to: string): string {
  return `${from}->${to}`;
}

/** The fields of node `id` of `kind` that its card shows and is sized from:
 *  ports and inline params from `desc` with `values` applied, or a note's
 *  text. An unknown kind (no `desc`) gets a portless card. */
export function cardContent(
  id: string,
  kind: string,
  desc: NodeDescriptor | undefined,
  values: Readonly<Record<string, string>>,
): Pick<CanvasNode, "id" | "kind" | "inputs" | "outputs" | "params" | "noteText"> {
  if (kind === NOTE_KIND) {
    const noteText = values["text"] ?? desc?.params.find((p) => p.name === "text")?.default ?? "";
    return { id, kind, inputs: [], outputs: [], params: [], noteText };
  }
  return {
    id,
    kind,
    inputs: (desc?.inputs ?? []).map((p) => ({ name: p.name, type: p.type })),
    outputs: (desc?.outputs ?? []).map((p) => ({ name: p.name, type: p.type })),
    params: inlineParams(desc?.params ?? [], values),
  };
}

/** The size a node of `desc` gets when it is added as `id`, before any
 *  value is set: what the app places a new card by. */
export const newNodeSize = (desc: NodeDescriptor, id: string): Size => nodeSize(cardContent(id, desc.kind, desc, {}));

/** Builds the drawable model; catalog gaps degrade to portless nodes. A node
 *  with no saved position goes to the first free spot (freePosition) among
 *  the nodes before it, at its real size, in document order. */
export function buildCanvasModel(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  preview: string | null = state.selection.at(-1) ?? null,
  results: PortResultsView = NO_PORT_RESULTS,
): CanvasModel {
  const selected = new Set(state.selection);
  const contributing = upstreamIds(state.document,preview);
  const cards = state.document.structure.nodes.map((n) => {
    const desc = catalog.get(n.kind);
    const content = cardContent(n.id, n.kind, desc, state.document.values[n.id] ?? {});
    const layout = state.document.layout[n.id];
    return { desc, content, layout, size: nodeSize(content, layout ?? {}) };
  });
  const placed: NodeBounds[] = cards.flatMap((c) => (c.layout ? [{ x: c.layout.x, y: c.layout.y, ...c.size }] : []));
  const nodes = cards.map(({ desc, content, layout, size }): CanvasNode => {
    const pos = layout ?? freePosition(placed, size.w, size.h);
    if (!layout) placed.push({ ...pos, ...size });
    const common = { x: pos.x, y: pos.y, ...size, ...content, selected: selected.has(content.id), contributing: contributing.has(content.id) };
    if (content.kind === NOTE_KIND) return common;
    return {
      ...common,
      status: state.evalState[content.id]?.status,
      badge: nodeBadge({ edges: state.document.structure.edges, evalState: state.evalState }, content.id),
      ...(desc?.description ? { description: desc.description } : {}),
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
