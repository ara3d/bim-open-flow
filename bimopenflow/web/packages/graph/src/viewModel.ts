// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { inlineParams, placeSlots, type CanvasParam } from "./canvasSlots.js";
import { clampNodeSize, type Size } from "./canvasResize.js";
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

export const NODE_WIDTH = 184;
/** Nodes with inline param slots get extra width so field values stay legible. */
export const WIDE_NODE_WIDTH = 260;
export const PORT_SPACING = 24;
export const NODE_HEADER = 46;

export function nodeWidth(params: readonly CanvasParam[]): number {
  return params.length > 0 ? WIDE_NODE_WIDTH : NODE_WIDTH;
}

/** A comment pinned to the canvas (see NoteNode in BimOpenFlow.Nodes.Viz): no
 *  ports, one text param drawn as the whole card rather than a labelled row. */
export const NOTE_KIND = "view.note";
export const NOTE_WIDTH = 300;
export const NOTE_LINE_H = 18;
export const NOTE_MAX_LINES = 12;
export const NOTE_PAD = 16;
/** Rough characters per wrapped line at the note's width and font size; only
 *  used to size the card. canvasParts wraps the actual text precisely for
 *  painting, so a note is always at least as tall as it needs and at most
 *  NOTE_MAX_LINES tall (canvasParts then ellipsizes past that). */
const NOTE_CHARS_PER_LINE = 34;

/** Fixed width, height from an approximate wrap of `text` up to
 *  NOTE_MAX_LINES lines (one more line for an explicit line break). */
export function noteHeight(text: string): number {
  const paragraphs = text.split("\n");
  const lines = paragraphs.reduce(
    (sum, p) => sum + Math.max(1, Math.ceil(p.length / NOTE_CHARS_PER_LINE)),
    0,
  );
  return NOTE_PAD * 2 + Math.min(Math.max(lines, 1), NOTE_MAX_LINES) * NOTE_LINE_H;
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

/** The fields of a card its default size is computed from. */
type SizedNode = Pick<CanvasNode, "kind" | "inputs" | "outputs" | "params" | "noteText">;

/** The size `node`'s content needs: its default size, and the smallest a
 *  resize may make it (TKT-124). */
export function contentSize(node: SizedNode): Size {
  return node.kind === NOTE_KIND
    ? { w: NOTE_WIDTH, h: noteHeight(node.noteText ?? "") }
    : { w: nodeWidth(node.params), h: nodeHeight(node.inputs.length, node.outputs.length, node.params) };
}

/** The size `node` is drawn at when `want` is asked for (a saved layout
 *  entry or a resize drag): see canvasResize.clampNodeSize. Only a note's
 *  height is resizable. */
export function fitNodeSize(node: SizedNode, want: { readonly w?: number; readonly h?: number }): Size {
  return clampNodeSize(contentSize(node), want, node.kind === NOTE_KIND);
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
      const note = { kind: n.kind, inputs: [], outputs: [], params: [], noteText };
      return {
        id: n.id,
        x: pos.x,
        y: pos.y,
        ...fitNodeSize(note, layout ?? {}),
        ...note,
        selected: selected.has(n.id),
        contributing: contributing.has(n.id),
      };
    }
    const card = {
      kind: n.kind,
      inputs: inputs.map((p) => ({ name: p.name, type: p.type })),
      outputs: outputs.map((p) => ({ name: p.name, type: p.type })),
      params,
    };
    return {
      id: n.id,
      x: pos.x,
      y: pos.y,
      ...fitNodeSize(card, layout ?? {}),
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
