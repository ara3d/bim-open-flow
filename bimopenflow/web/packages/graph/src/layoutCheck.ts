// Checks a drawn graph for the layout faults a reader notices at once: cards
// that overlap, wires that run behind a third card, wires that run backwards
// (right to left), and nodes that nothing is wired to. Pure and gratify-light
// (only the wire curve), so a test, a script, and the app's open-time check
// all share it.

import { wireAt } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import { nodeFootprint } from "./nodeRender.js";
import { NOTE_KIND } from "./nodeSize.js";
import { endpointPos } from "./portGeometry.js";
import type { CanvasModel, CanvasNode } from "./viewModel.js";

/** `handPlaced` is reported by scripts/layoutReport.ts for a committed sample, not by the checks here. */
export type LayoutIssueKind = "overlap" | "wireBehindCard" | "backwardWire" | "unwiredInput" | "isolated" | "handPlaced";

export interface LayoutIssue {
  readonly kind: LayoutIssueKind;
  /** One line a person can act on, naming the nodes or the edge. */
  readonly text: string;
}

interface Box { readonly x: number; readonly y: number; readonly w: number; readonly h: number }

/** The canvas-space box `node` paints (nodeRender.nodeFootprint). */
export function paintedBox(node: CanvasNode): Box {
  const f = nodeFootprint(node);
  return { x: node.x + f.x, y: node.y + f.y, w: f.w, h: f.h };
}

const intersects = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

/** A wire may graze a card's edge: points within this much of the border
 *  do not count as behind it. */
const WIRE_GRAZE = 6;
/** Samples along a wire's curve. */
const WIRE_SAMPLES = 32;
/** A wire whose target socket is this far left of its source runs backwards. */
const BACKWARD_SLACK = 8;

const nodeOf = (endpoint: string) => endpoint.slice(0, endpoint.indexOf("."));

/** The layout faults of `model` as drawn: overlapping cards, wires behind a
 *  card that is neither of their ends, and wires running backwards. */
export function layoutIssues(model: CanvasModel): LayoutIssue[] {
  const issues: LayoutIssue[] = [];
  const boxes = model.nodes.map((n) => ({ id: n.id, box: paintedBox(n) }));
  for (const [i, a] of boxes.entries())
    for (const b of boxes.slice(i + 1))
      if (intersects(a.box, b.box)) issues.push({ kind: "overlap", text: `${a.id} / ${b.id}` });
  const railed = new Set(model.nodes.filter((n) => n.inRail).map((n) => n.id));
  for (const edge of model.edges) {
    // A rail card (a graph input, an unwired source) is placed by the editor,
    // not the stored layout, so where its wire runs is not the layout's fault
    // to fix; routing it clear is TKT-287's.
    if (railed.has(nodeOf(edge.from))) continue;
    const from = endpointPos(model.nodes, edge.from, "out");
    const to = endpointPos(model.nodes, edge.to, "in");
    if (!from || !to) continue;
    if (to.x < from.x - BACKWARD_SLACK) issues.push({ kind: "backwardWire", text: `${edge.from} -> ${edge.to}` });
    const ends = new Set([nodeOf(edge.from), nodeOf(edge.to)]);
    const points = Array.from({ length: WIRE_SAMPLES + 1 }, (_, i) => wireAt(from, to, i / WIRE_SAMPLES));
    for (const { id, box } of boxes) {
      if (ends.has(id)) continue;
      const inner = { x: box.x + WIRE_GRAZE, y: box.y + WIRE_GRAZE, w: box.w - 2 * WIRE_GRAZE, h: box.h - 2 * WIRE_GRAZE };
      if (points.some((p) => p.x > inner.x && p.x < inner.x + inner.w && p.y > inner.y && p.y < inner.y + inner.h))
        issues.push({ kind: "wireBehindCard", text: `${edge.from} -> ${edge.to} behind ${id}` });
    }
  }
  return issues;
}

/** The faults that make a stored layout unusable as it stands: the app lays
 *  such a graph out afresh when it opens it. */
export const isBrokenLayout = (model: CanvasModel): boolean =>
  layoutIssues(model).some((i) => i.kind === "overlap");

/** The wiring faults of `document`: an input port that is not optional and
 *  has no wire, and a node with no wire at all in a graph of several nodes.
 *  Notes, and a graph of one node, are never isolated. */
export function connectionIssues(document: GraphDocument, catalog: ReadonlyMap<string, NodeDescriptor>): LayoutIssue[] {
  const { nodes, edges } = document.structure;
  const wiredTo = new Set(edges.map((e) => e.to));
  const touched = new Set(edges.flatMap((e) => [nodeOf(e.from), nodeOf(e.to)]));
  const working = nodes.filter((n) => n.kind !== NOTE_KIND);
  const issues: LayoutIssue[] = [];
  for (const node of working) {
    const desc = catalog.get(node.kind);
    for (const port of desc?.inputs ?? [])
      if (!port.optional && !wiredTo.has(`${node.id}.${port.name}`))
        issues.push({ kind: "unwiredInput", text: `${node.id}.${port.name} (${node.kind})` });
    if (working.length > 1 && !touched.has(node.id)) issues.push({ kind: "isolated", text: `${node.id} (${node.kind})` });
  }
  return issues;
}
