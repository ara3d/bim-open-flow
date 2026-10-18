// The store actions behind a pane's addNodes and setParams (TKT-320): the same
// actions a palette gesture dispatches, so a pane's edit is one undo step and
// leaves a graph a person could have built. Pure: the caller dispatches.

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import type { PaneContext } from "@bimopenflow/panes";
import { portTypesCompatible } from "@bimopenflow/graph";
import { reduce, type Action, type State } from "@bimopenflow/state";
import { addNodeActions, freeSpot } from "./addNodePlan.js";

/** A node a pane asks for (PlannedNode in the panes package). */
export type PlannedNode = Parameters<NonNullable<PaneContext["addNodes"]>>[0][number];

/** The actions that add `nodes`, and the ids they get. One `batch`. */
export interface NodePlan {
  ids: string[];
  action: Action;
}

const names = (list: readonly string[]): string => (list.length > 0 ? list.join(", ") : "none");

/** The wire type of a kind's input, or of a bindable parameter of that name; throws a sentence naming the choices. */
function inputType(desc: NodeDescriptor, port: string): PortType {
  const bindable = desc.params.filter((p) => p.bindable !== undefined);
  const found = desc.inputs.find((i) => i.name === port) ?? bindable.find((p) => p.name === port);
  const type = found && ("type" in found ? found.type : found.bindable);
  if (type === undefined)
    throw new Error(`${desc.kind} has no input '${port}'; its inputs are: ${names([...desc.inputs, ...bindable].map((i) => i.name))}`);
  return type;
}

function outputType(desc: NodeDescriptor, port: string): PortType {
  const found = desc.outputs.find((o) => o.name === port);
  if (!found) throw new Error(`${desc.kind} has no output '${port}'; its outputs are: ${names(desc.outputs.map((o) => o.name))}`);
  return found.type;
}

/** Throws a sentence a pane can show unless `from` (a node and output port) may feed `to`'s input. */
function checkWire(from: NodeDescriptor, fromPort: string, to: NodeDescriptor, toPort: string): void {
  const out = outputType(from, fromPort);
  const into = inputType(to, toPort);
  if (!portTypesCompatible(out, into))
    throw new Error(`${from.kind} output '${fromPort}' is a ${out}, but ${to.kind} input '${toPort}' takes a ${into}`);
}

/** The descriptor of a node already in the graph; throws when the node or its kind is missing. */
function existingDescriptor(state: State, catalog: ReadonlyMap<string, NodeDescriptor>, nodeId: string): NodeDescriptor {
  const node = state.document.structure.nodes.find((n) => n.id === nodeId);
  if (!node) throw new Error(`No node '${nodeId}' in the open graph`);
  const desc = catalog.get(node.kind);
  if (!desc) throw new Error(`Unknown node kind '${node.kind}'`);
  return desc;
}

/** Throws a sentence naming the first of `paramNames` that `desc` does not declare. */
function checkParamNames(desc: NodeDescriptor, paramNames: Iterable<string>): void {
  for (const name of paramNames)
    if (!desc.params.some((p) => p.name === name)) throw new Error(`${desc.kind} has no parameter '${name}'`);
}

/**
 * Plans the nodes in order: each is added at the first free spot (earlier
 * planned nodes count as occupied), its params set, its inputs connected, and
 * the last one selected. Throws on an unknown kind or a bad reference.
 */
export function planNodes(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  nodes: readonly PlannedNode[],
): NodePlan {
  const ids: string[] = [];
  const descs: NodeDescriptor[] = [];
  const actions: Action[] = [];
  let running = state;
  nodes.forEach((planned, index) => {
    const desc = catalog.get(planned.kind);
    if (!desc) throw new Error(`Unknown node kind '${planned.kind}'`);
    // addNodeActions ends with a select; only the last node keeps one.
    checkParamNames(desc, Object.keys(planned.params ?? {}));
    const adds = addNodeActions(running, desc, freeSpot(running, catalog, desc)).filter((a) => a.type !== "select");
    const id = (adds[0] as { id: string }).id;
    const own: Action[] = [
      ...adds,
      ...Object.entries(planned.params ?? {}).map(([name, value]): Action => ({ type: "setParam", nodeId: id, name, value })),
    ];
    for (const [port, source] of Object.entries(planned.inputs ?? {})) {
      const from = "node" in source ? ids[source.node] : source.nodeId;
      if (from === undefined) throw new Error(`Input '${port}' of node ${index} refers to a node that is not planned before it`);
      const fromDesc = "node" in source ? descs[source.node]! : existingDescriptor(running, catalog, source.nodeId);
      checkWire(fromDesc, source.port, desc, port);
      own.push({ type: "connect", from: `${from}.${source.port}`, to: `${id}.${port}` });
    }
    running = own.reduce(reduce, running);
    ids.push(id);
    descs.push(desc);
    actions.push(...own);
  });
  if (ids.length > 0) actions.push({ type: "select", ids: [ids[ids.length - 1]!] });
  return { ids, action: { type: "batch", actions } };
}

/** One `batch` of setParam, one per entry; throws when the node is not in the document or a name is not one of its kind's params. */
export function planSetParams(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  nodeId: string,
  params: Record<string, string>,
): Action {
  checkParamNames(existingDescriptor(state, catalog, nodeId), Object.keys(params));
  return {
    type: "batch",
    actions: Object.entries(params).map(([name, value]): Action => ({ type: "setParam", nodeId, name, value })),
  };
}
