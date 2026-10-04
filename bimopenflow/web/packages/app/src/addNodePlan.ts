// The store actions for adding one node from the palette or the sidebar
// (TKT-96): add, place, select, and optionally connect, dispatched as one
// `batch` so the whole gesture is one undo step. Pure. Placement uses the
// size the new card will be drawn at (graph's newNodeSize, TKT-125).

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Action, State } from "@bimopenflow/state";
import type { AnchorRef } from "@bimopenflow/graph";
import { buildCanvasModel, freePosition, newNodeSize, portY } from "@bimopenflow/graph";
import { freshNodeId } from "./ids.js";

/** The id a node of `desc` gets when it is added to `state`. */
const nextNodeId = (state: State, desc: NodeDescriptor): string =>
  freshNodeId(desc.kind, state.document.structure.nodes.map((n) => n.id));

/** The first spot where a node of `desc` added to `state` overlaps no card,
 *  every card measured at the size the canvas draws it. */
export function freeSpot(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  desc: NodeDescriptor,
): { x: number; y: number } {
  const size = newNodeSize(desc, nextNodeId(state, desc));
  return freePosition(buildCanvasModel(state, catalog).nodes, size.w, size.h);
}

/**
 * The actions that add `desc` at (x, y), select it, and optionally connect it
 * to `wire`, in the order addNode, setLayout, select, connect. From an output
 * anchor the wire lands on the new node's `port` input, and the node's left
 * edge sits at x; from an input anchor it leaves the new node's `port`
 * output, and the node's right edge sits at x. With a wire, y is the
 * connecting socket's height; without one, (x, y) is the top-left corner.
 */
export function addNodeActions(
  state: State,
  desc: NodeDescriptor,
  at: { x: number; y: number },
  wire?: { from: AnchorRef; port: string },
): Action[] {
  const id = nextNodeId(state, desc);
  const add: Action[] = [
    { type: "addNode", id, kind: desc.kind, version: desc.version },
    { type: "setLayout", nodeId: id, layout: wire ? wiredPosition(desc, id, at, wire) : { x: at.x, y: at.y } },
    { type: "select", ids: [id] },
  ];
  if (!wire) return add;
  const own = `${id}.${wire.port}`;
  const [from, to] = wire.from.dir === "out" ? [wire.from.endpoint, own] : [own, wire.from.endpoint];
  return [...add, { type: "connect", from, to }];
}

/** The top-left corner that puts the connecting socket on `at`. */
function wiredPosition(
  desc: NodeDescriptor,
  id: string,
  at: { x: number; y: number },
  wire: { from: AnchorRef; port: string },
): { x: number; y: number } {
  const intoInput = wire.from.dir === "out";
  const ports = intoInput ? desc.inputs : desc.outputs;
  const index = Math.max(0, ports.findIndex((p) => p.name === wire.port));
  const width = newNodeSize(desc, id).w;
  return { x: intoInput ? at.x : at.x - width, y: at.y - portY(0, index) };
}
