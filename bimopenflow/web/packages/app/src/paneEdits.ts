// The store actions behind a pane's addNodes and setParams (TKT-320): the same
// actions a palette gesture dispatches, so a pane's edit is one undo step and
// leaves a graph a person could have built. Pure: the caller dispatches.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { PaneContext } from "@bimopenflow/panes";
import { reduce, type Action, type State } from "@bimopenflow/state";
import { addNodeActions, freeSpot } from "./addNodePlan.js";

/** A node a pane asks for (PlannedNode in the panes package). */
export type PlannedNode = Parameters<NonNullable<PaneContext["addNodes"]>>[0][number];

/** The actions that add `nodes`, and the ids they get. One `batch`. */
export interface NodePlan {
  ids: string[];
  action: Action;
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
  const actions: Action[] = [];
  let running = state;
  nodes.forEach((planned, index) => {
    const desc = catalog.get(planned.kind);
    if (!desc) throw new Error(`Unknown node kind '${planned.kind}'`);
    // addNodeActions ends with a select; only the last node keeps one.
    const adds = addNodeActions(running, desc, freeSpot(running, catalog, desc)).filter((a) => a.type !== "select");
    const id = (adds[0] as { id: string }).id;
    const own: Action[] = [
      ...adds,
      ...Object.entries(planned.params ?? {}).map(([name, value]): Action => ({ type: "setParam", nodeId: id, name, value })),
    ];
    for (const [port, source] of Object.entries(planned.inputs ?? {})) {
      const from = "node" in source ? ids[source.node] : source.nodeId;
      if (from === undefined) throw new Error(`Input '${port}' of node ${index} refers to a node that is not planned before it`);
      own.push({ type: "connect", from: `${from}.${source.port}`, to: `${id}.${port}` });
    }
    running = own.reduce(reduce, running);
    ids.push(id);
    actions.push(...own);
  });
  if (ids.length > 0) actions.push({ type: "select", ids: [ids[ids.length - 1]!] });
  return { ids, action: { type: "batch", actions } };
}

/** One `batch` of setParam, one per entry; throws when the node is not in the document. */
export function planSetParams(state: State, nodeId: string, params: Record<string, string>): Action {
  if (!state.document.structure.nodes.some((n) => n.id === nodeId))
    throw new Error(`No node '${nodeId}' in the open graph`);
  return {
    type: "batch",
    actions: Object.entries(params).map(([name, value]): Action => ({ type: "setParam", nodeId, name, value })),
  };
}
