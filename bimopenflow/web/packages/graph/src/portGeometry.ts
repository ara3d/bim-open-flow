// Socket geometry shared by drawing (canvasParts.ts) and the peek hit-test:
// the world position of a node's port row, and the point-to-endpoint lookup
// the hover listener uses. Viz-free: no gratify import, so it is testable
// headless.

import { wireDist } from "gratify";
import type { CanvasModel, CanvasNode } from "./viewModel.js";
import { NODE_HEADER, PORT_SPACING } from "./viewModel.js";
import type { ParamKind, PortType } from "@bimopenflow/contracts";
import { PARAM_SOCKET_DY, placeSlots } from "./canvasSlots.js";
import { portRowsBottom } from "./nodeSize.js";

/** Radius within which a pointer grabs a socket to drag a wire or peek it:
 *  world units at zoom 1 and above, screen pixels below (grabRadius). Moved
 *  from canvasParts.ts so drawing, wire-dragging, and the peek hit-test
 *  share one definition. */
export const SOCKET_GRAB_RADIUS = 12;

/** The world-unit radius of a grab target `radius` pixels across at `zoom`
 *  (TKT-125): the same world size at zoom 1 and above, and the same screen
 *  size when zoomed out, so a socket or a card corner stays grabbable at a
 *  fitted zoom of 0.28 instead of shrinking under 3 pixels. */
export const grabRadius = (radius: number, zoom: number): number => radius / Math.min(zoom, 1);
/** Distance, in world units, within which a pointer is considered "on" a
 *  wire. Moved from canvasParts.ts's Wire.hit. */
export const WIRE_HIT_DISTANCE = 8;

/** World y of the index-th port row of a node whose top is `top`. Mirrors
 *  canvasParts.ts's own `portY`, which this module is the source of truth
 *  for once C7 imports it from here. */
export function portY(top: number, index: number): number {
  return top + NODE_HEADER + (index + 0.5) * PORT_SPACING;
}

/** The input socket of a parameter: its name (the edge targets
 *  `node.<name>`), the wire type it takes when the parameter is bindable,
 *  the parameter's kind (a graph input of that kind may feed it), and its y
 *  offset below the card's top. */
export interface ParamSocket {
  readonly name: string;
  readonly type?: PortType;
  readonly kind: ParamKind;
  readonly dy: number;
}

/** The sockets of `node`'s parameters, one per row, on the left edge beside
 *  the row: a bindable parameter's, which a wire of its type may feed, and a
 *  FilePath parameter's, which a graph input may feed (its `$name` reference
 *  is drawn as a wire). Any other parameter has none. */
export function paramSockets(
  node: Pick<CanvasNode, "inputs" | "outputs" | "params">,
): ParamSocket[] {
  const { slots } = placeSlots(node.params, portRowsBottom(node.inputs.length, node.outputs.length));
  return slots.flatMap(({ param, y }) =>
    param.bindable || param.kind === "FilePath"
      ? [{ name: param.name, ...(param.bindable ? { type: param.bindable } : {}), kind: param.kind, dy: y + PARAM_SOCKET_DY }]
      : []);
}

/** Where `node`'s socket for `port` sits relative to the card's top-left
 *  corner, or undefined if the node has no such port on that side. Layouts
 *  use it to line wires up before the node has a position. */
export function socketOffset(
  node: Pick<CanvasNode, "w" | "inputs" | "outputs" | "params">,
  port: string,
  dir: "in" | "out",
): { x: number; y: number } | undefined {
  const ports = dir === "in" ? node.inputs : node.outputs;
  const index = ports.findIndex((p) => p.name === port);
  if (index < 0) {
    const param = dir === "in" ? paramSockets(node).find((s) => s.name === port) : undefined;
    return param ? { x: 0, y: param.dy } : undefined;
  }
  return { x: dir === "in" ? 0 : node.w, y: portY(0, index) };
}

/** World position of `node`'s socket for `port`, or undefined if the node
 *  has no such port on that side. */
function socketPos(
  node: CanvasNode,
  port: string,
  dir: "in" | "out",
): { x: number; y: number } | undefined {
  const at = socketOffset(node, port, dir);
  return at && { x: node.x + at.x, y: node.y + at.y };
}

/** World position of the socket `endpoint` ("node.port") names, or
 *  undefined when no node or port matches. */
export function endpointPos(
  nodes: readonly CanvasNode[],
  endpoint: string,
  dir: "in" | "out",
): { x: number; y: number } | undefined {
  const dot = endpoint.indexOf(".");
  if (dot < 0) return undefined;
  const nodeId = endpoint.slice(0, dot);
  const port = endpoint.slice(dot + 1);
  const node = nodes.find((n) => n.id === nodeId);
  if (!node) return undefined;
  return socketPos(node, port, dir);
}

/** The output endpoint whose socket lies within `radius` of (x, y) in world
 *  coords, nearest first; else the source endpoint of the wire within
 *  WIRE_HIT_DISTANCE; else null. Input sockets are never targets. */
export function peekTargetAt(
  model: CanvasModel,
  x: number,
  y: number,
  radius: number = SOCKET_GRAB_RADIUS,
): string | null {
  const point = { x, y };
  let bestEndpoint: string | null = null;
  let bestDist = Infinity;
  for (const node of model.nodes) {
    node.outputs.forEach((port, index) => {
      const pos = { x: node.x + node.w, y: portY(node.y, index) };
      const dist = Math.hypot(pos.x - x, pos.y - y);
      if (dist <= radius && dist < bestDist) {
        bestEndpoint = `${node.id}.${port.name}`;
        bestDist = dist;
      }
    });
  }
  if (bestEndpoint) return bestEndpoint;

  for (const edge of model.edges) {
    const a = endpointPos(model.nodes, edge.from, "out");
    const b = endpointPos(model.nodes, edge.to, "in");
    if (a && b && wireDist(a, b, point) < WIRE_HIT_DISTANCE) return edge.from;
  }
  return null;
}
