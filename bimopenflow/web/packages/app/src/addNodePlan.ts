// The store actions for adding one node from the palette or the sidebar
// (TKT-96): add, place, select, and optionally connect, dispatched as one
// `batch` so the whole gesture is one undo step. Pure.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { Action, State } from "@bimopenflow/state";
import type { AnchorRef } from "./canvasIntents.js";
import { inlineParams } from "./canvasSlots.js";
import { freshNodeId } from "./ids.js";
import { portY } from "./portGeometry.js";
import { nodeWidth } from "./viewModel.js";

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
  const id = freshNodeId(desc.kind, state.document.structure.nodes.map((n) => n.id));
  const add: Action[] = [
    { type: "addNode", id, kind: desc.kind, version: desc.version },
    { type: "setLayout", nodeId: id, layout: wire ? wiredPosition(desc, at, wire) : { x: at.x, y: at.y } },
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
  at: { x: number; y: number },
  wire: { from: AnchorRef; port: string },
): { x: number; y: number } {
  const intoInput = wire.from.dir === "out";
  const ports = intoInput ? desc.inputs : desc.outputs;
  const index = Math.max(0, ports.findIndex((p) => p.name === wire.port));
  const width = nodeWidth(inlineParams(desc.params, {}));
  return { x: intoInput ? at.x : at.x - width, y: at.y - portY(0, index) };
}
