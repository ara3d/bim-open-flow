// Pure canvas view-model: store State + node catalog -> what the gratify
// canvas draws. Gratify-free so it is testable headless.

import type { NodeDescriptor, NodeStatus, PortSchema, PortType } from "@bimopenflow/contracts";
import { schemaFeeding, schemaMessages, type NodeLayout, type State } from "@bimopenflow/state";
import { inlineParams, type CanvasParam } from "./canvasSlots.js";
import { nodeSize, NOTE_KIND, type Size } from "./nodeSize.js";
import { currentDescriptionLevel, descriptionText, type DescriptionLevel } from "./descriptionLevel.js";
import { upstreamIds } from "./graphPreview";
import { wiredPreviews } from "./wiredParams.js";
import { nodeBadge, type NodeBadge } from "./nodeBadge.js";
import { inputCardId, INPUT_KIND, INPUT_PORT, inputParam, inputPortType, inputUses, markInputRefs, type InputMark } from "./graphInputs.js";
import { NO_SOURCE_VIEW, railSources, sourceNodeIds, type SourceView } from "./sources.js";
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
  /** The part of the kind's catalog description the page's DescriptionLevel
   *  shows (TKT-98, TKT-278), wrapped at the bottom of the card so a reader
   *  learns what the node does without the sidebar. Absent at the hidden
   *  level, and for a note or an unknown kind. */
  readonly description?: string;
  /** The kind writes a file on Run (catalog capability Effect): the card
   *  carries the writer glyph, so the one gesture that touches disk is
   *  visible before anyone presses Run. */
  readonly effect?: true;
  readonly selected: boolean;
  readonly contributing?: boolean;
  /** The `text` param's value, only for a `view.note` node (TKT-82): drawn as
   *  a sticky note instead of the normal ports/params card, so its text does
   *  not also travel through CanvasParam. */
  readonly noteText?: string;
  /** Drawn in the sources rail (TKT-138): x and y are the rail's, not the saved layout's. */
  readonly inRail?: boolean;
  /** Set on the card of a graph input (TKT-204), which is not a node of the document:
   *  its one parameter is the input's current value, and editing it goes to the host. */
  readonly input?: InputMark;
}

export interface CanvasEdge {
  readonly id: string; // "from->to", stable across rebuilds
  readonly from: string; // "nodeId.port"
  readonly to: string;
  readonly contributing?: boolean;
  /** Row count of the table or relation leaving `from`; absent when uncounted. */
  readonly rows?: WireRows;
  /** What travels on the wire, from the catalog: a Relation is drawn hollow so
   *  it reads apart from a Table. Absent for an unknown kind or port. */
  readonly type?: PortType;
  /** The wire ends on a parameter socket, not an input port: it carries a
   *  resource or a value a source holds (a model path, a database), not rows,
   *  and is drawn thinner and quieter than a table wire. */
  readonly param?: true;
  /** The wire is a graph input's `$name` reference held in the parameter at
   *  `to`, not an edge of the document (TKT-204): cutting it inlines the value. */
  readonly ref?: true;
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

/** The schema wired into the first Table or Relation input of the node that
 *  owns the output `endpoint`, when the static pass knows it. */
export function feedingSchema(state: State, catalog: ReadonlyMap<string, NodeDescriptor>, endpoint: string): PortSchema | undefined {
  const nodeId = endpoint.slice(0, endpoint.indexOf("."));
  const node = state.document.structure.nodes.find((n) => n.id === nodeId);
  const input = node && catalog.get(node.kind)?.inputs.find((p) => p.type === "Table" || p.type === "Relation");
  return input && schemaFeeding(state, `${nodeId}.${input.name}`);
}

/** True when `endpoint` ("nodeId.name") names a parameter socket of a known
 *  kind rather than one of its input ports. */
export function isParamSocket(state: State, catalog: ReadonlyMap<string, NodeDescriptor>, endpoint: string): boolean {
  const dot = endpoint.indexOf(".");
  const node = state.document.structure.nodes.find((n) => n.id === endpoint.slice(0, dot));
  const desc = node && catalog.get(node.kind);
  return !!desc && !desc.inputs.some((p) => p.name === endpoint.slice(dot + 1));
}

/** The catalog type of the output `endpoint` ("nodeId.port") names, if known. */
export function outputType(state: State, catalog: ReadonlyMap<string, NodeDescriptor>, endpoint: string): PortType | undefined {
  const dot = endpoint.indexOf(".");
  const node = state.document.structure.nodes.find((n) => n.id === endpoint.slice(0, dot));
  return node && catalog.get(node.kind)?.outputs.find((p) => p.name === endpoint.slice(dot + 1))?.type;
}

/** The fields of a card its size is computed from. */
type CardContent = Pick<CanvasNode, "id" | "kind" | "inputs" | "outputs" | "params" | "noteText" | "description" | "effect">;

/** `text` cut to `level` as a card field: absent when nothing is shown. */
const describedAs = (text: string | undefined, level: DescriptionLevel): Pick<CanvasNode, "description"> => {
  const shown = descriptionText(text ?? "", level);
  return shown ? { description: shown } : {};
};

/** The fields of node `id` of `kind` that its card shows and is sized from:
 *  ports and inline params from `desc` with `values` applied (and the
 *  previews in `wired` for the parameters an edge feeds), the description
 *  `level` shows, or a note's text. An unknown kind (no `desc`) gets a
 *  portless card. */
export function cardContent(
  id: string,
  kind: string,
  desc: NodeDescriptor | undefined,
  values: Readonly<Record<string, string>>,
  wired: Readonly<Record<string, string>> = {},
  level: DescriptionLevel = currentDescriptionLevel(),
): CardContent {
  if (kind === NOTE_KIND) {
    const noteText = values["text"] ?? desc?.params.find((p) => p.name === "text")?.default ?? "";
    return { id, kind, inputs: [], outputs: [], params: [], noteText };
  }
  return {
    id,
    kind,
    inputs: (desc?.inputs ?? []).map((p) => ({ name: p.name, type: p.type })),
    outputs: (desc?.outputs ?? []).map((p) => ({ name: p.name, type: p.type })),
    params: inlineParams(desc?.params ?? [], values, wired),
    ...describedAs(desc?.description, level),
    ...(desc?.capability === "Effect" ? { effect: true as const } : {}),
  };
}

/** The size a node of `desc` gets when it is added as `id`, before any
 *  value is set, at the page's description level: what the app places a new
 *  card by. */
export const newNodeSize = (desc: NodeDescriptor, id: string): Size => nodeSize(cardContent(id, desc.kind, desc, {}));

/** What a graph input's card says it is, in place of a catalog description. */
const INPUT_DESCRIPTION = "Graph input";

/** Space between the rail and the nodes to its right, and between two rail cards. */
const RAIL_GAP = 48;
const RAIL_STACK_GAP = 16;
const RAIL_TOP = 24;

/** What the editor tells buildCanvasModel besides the document. */
export interface ModelOptions {
  /** How source nodes are drawn; default none of it (every node a plain card). */
  readonly sources?: SourceView;
  /** How much of each description the cards show; default the page's (currentDescriptionLevel). */
  readonly descriptions?: DescriptionLevel;
}

/** Rail positions for cards of the given sizes, stacked in one column left of `others`, from their top. */
function railPositions(rail: readonly Size[], others: readonly NodeBounds[]): { x: number; y: number }[] {
  const left = others.length ? Math.min(...others.map((r) => r.x)) : RAIL_GAP + RAIL_TOP;
  const x = left - RAIL_GAP - Math.max(0, ...rail.map((s) => s.w));
  let y = others.length ? Math.min(...others.map((r) => r.y)) : RAIL_TOP;
  return rail.map((s) => {
    const at = { x, y };
    y += s.h + RAIL_STACK_GAP;
    return at;
  });
}

/** A node of the document or a graph input, before it is placed. */
interface Card {
  readonly content: CardContent;
  readonly layout: NodeLayout | undefined;
  readonly size: Size;
  readonly inRail: boolean;
  readonly input?: InputMark;
}

/** Builds the drawable model; catalog gaps degrade to portless nodes. A node
 *  with no saved position goes to the first free spot (freePosition) among
 *  the nodes before it, at its real size, in document order. With
 *  `options.sources.rail` the unwired source nodes leave that flow and stack
 *  in a rail. */
export function buildCanvasModel(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  preview: string | null = state.selection.at(-1) ?? null,
  results: PortResultsView = NO_PORT_RESULTS,
  options: ModelOptions = {},
): CanvasModel {
  const view = options.sources ?? NO_SOURCE_VIEW;
  const level = options.descriptions ?? currentDescriptionLevel();
  const selected = new Set(state.selection);
  const contributing = upstreamIds(state.document,preview);
  const sources = sourceNodeIds(state.document, catalog);
  const railed = view.rail ? railSources(state.document, sources) : new Set<string>();
  const cards: Card[] = state.document.structure.nodes.map((n) => {
    const desc = catalog.get(n.kind);
    const content = cardContent(n.id, n.kind, desc, state.document.values[n.id] ?? {}, wiredPreviews(state.document, catalog, n.id, desc), level);
    const params = markInputRefs(state, content.params);
    const layout = state.document.layout[n.id];
    const marked = { ...content, params };
    return { content: marked, layout, size: nodeSize(marked, layout ?? {}), inRail: railed.has(n.id) };
  });
  const inputCards = (state.document.structure.inputs ?? []).map((decl): Card => {
    const content = {
      id: inputCardId(decl.name), kind: INPUT_KIND, inputs: [],
      outputs: [{ name: INPUT_PORT, type: inputPortType(decl.kind) }], params: [inputParam(state, catalog, decl)],
      ...describedAs(INPUT_DESCRIPTION, level),
    };
    return { content, layout: undefined, size: nodeSize(content), inRail: true, input: { name: decl.name, kind: decl.kind } };
  });
  cards.unshift(...inputCards);
  const flowing = cards.filter((c) => !c.inRail);
  const placed: NodeBounds[] = flowing.flatMap((c) => (c.layout ? [{ x: c.layout.x, y: c.layout.y, ...c.size }] : []));
  const spots = new Map<string, { x: number; y: number }>();
  for (const c of flowing) {
    const pos = c.layout ?? freePosition(placed, c.size.w, c.size.h);
    if (!c.layout) placed.push({ ...pos, ...c.size });
    spots.set(c.content.id, pos);
  }
  const rail = cards.filter((c) => c.inRail);
  railPositions(rail.map((c) => c.size), placed).forEach((pos, i) => spots.set(rail[i]!.content.id, pos));
  const nodes = cards.map(({ content, size, inRail, input }): CanvasNode => {
    const pos = spots.get(content.id)!;
    const common = {
      x: pos.x, y: pos.y, ...size, ...content,
      selected: selected.has(content.id), contributing: contributing.has(content.id),
      ...(inRail ? { inRail } : {}),
    };
    if (content.kind === NOTE_KIND) return common;
    if (input) return { ...common, input, selected: false, contributing: false };
    return {
      ...common,
      status: state.evalState[content.id]?.status,
      badge: nodeBadge({ edges: state.document.structure.edges, evalState: state.evalState }, content.id, schemaMessages(state, content.id).length),
    };
  });
  const edges = state.document.structure.edges.map((e) => {
    const rows = results.counts.get(e.from);
    const type = outputType(state, catalog, e.from);
    return {
      id: edgeId(e.from, e.to),
      from: e.from,
      to: e.to,
      contributing: contributing.has(e.from.split(".")[0]!) && contributing.has(e.to.split(".")[0]!),
      ...(rows ? { rows: state.dirty ? { ...rows, current: false } : rows } : {}),
      ...(type ? { type } : {}),
      ...(isParamSocket(state, catalog, e.to) ? { param: true as const } : {}),
    };
  });
  // A parameter holding `$name` is wired from the input's card, unless an edge already feeds it.
  const fed = new Set(edges.map((e) => e.to));
  const refs = inputUses(state).flatMap((u): CanvasEdge[] => {
    const from = `${inputCardId(u.input)}.${INPUT_PORT}`;
    const to = `${u.nodeId}.${u.param}`;
    return fed.has(to) ? [] : [{ id: edgeId(from, to), from, to, param: true, ref: true }];
  });
  return {
    nodes,
    edges: [...edges, ...refs],
    selectedEdgeId: null,
    openEditor: null,
    ...(results.peek ? { peek: withFeeding(results.peek, state, catalog) } : {}),
  };
}

/** `peek` with the schema feeding its node attached, when there is one. */
function withFeeding(peek: PortPeekView, state: State, catalog: ReadonlyMap<string, NodeDescriptor>): PortPeekView {
  const feeding = feedingSchema(state, catalog, peek.endpoint);
  return feeding ? { ...peek, feeding } : peek;
}
