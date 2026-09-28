// The node-status badge: display text plus, for a state that traces back to
// another node, the id of the upstream node responsible. Pure and
// gratify-free (TKT-10), so it is testable against a plain graph fixture —
// canvasParts.ts paints the result next to the existing status dot.

import type { NodeState, NodeStatus } from "@bimopenflow/contracts";

/** The slice of the graph the cause walk needs: port-ref edges ("nodeId.port")
 *  and the evaluation state per node id, as carried by the state package's
 *  `State.evalState` and `GraphDocument.structure.edges`. */
export interface BadgeGraph {
  readonly edges: readonly { readonly from: string; readonly to: string }[];
  readonly evalState: Readonly<Record<string, NodeState>>;
}

export interface NodeBadge {
  readonly status: NodeStatus;
  /** Short text painted on the node card. */
  readonly text: string;
  /** Id of the nearest upstream node whose own state explains this one's
   *  Unready, Unavailable, or Error status. Absent when the fault is local
   *  to this node (an unset parameter, or its own failed evaluation). */
  readonly causeNodeId?: string;
}

const nodeIdOf = (portRef: string): string => portRef.split(".")[0]!;

/**
 * Walks upstream from `nodeId` to find the node whose own state *is* the
 * cause, rather than the nearest node that is merely also affected: a
 * predecessor in Error or EffectPending is a root cause immediately: an
 * Unready or Unavailable predecessor is itself downstream of something, so
 * the walk continues through it and only reports it directly when it has no
 * upstream cause of its own (an unwired source, or an unset parameter).
 * An Ok predecessor is a healthy input and never the cause. `visited` guards
 * a cyclic graph (which the document model otherwise disallows) against an
 * infinite walk.
 */
function upstreamCause(
  graph: BadgeGraph,
  nodeId: string,
  visited: Set<string> = new Set([nodeId]),
): string | undefined {
  for (const edge of graph.edges) {
    if (nodeIdOf(edge.to) !== nodeId) continue;
    const fromId = nodeIdOf(edge.from);
    if (visited.has(fromId)) continue;
    visited.add(fromId);
    const state = graph.evalState[fromId];
    if (!state || state.status === "Ok") continue;
    if (state.status === "Error" || state.status === "EffectPending") return fromId;
    return upstreamCause(graph, fromId, visited) ?? fromId;
  }
  return undefined;
}

/**
 * The badge for one node: its status, the text to paint, and — for Unready,
 * Unavailable, or Error — the upstream node responsible when one exists.
 * Returns undefined when the node has no evaluation state yet (never
 * evaluated this session).
 */
export function nodeBadge(graph: BadgeGraph, nodeId: string): NodeBadge | undefined {
  const state = graph.evalState[nodeId];
  if (!state) return undefined;
  switch (state.status) {
    case "Ok":
      return { status: state.status, text: "Ok" };
    case "EffectPending":
      // Not an error and not blocked on anything — the node is simply an
      // effect, gated behind an explicit Run.
      return { status: state.status, text: "Run to see results" };
    case "Unavailable": {
      const causeNodeId = upstreamCause(graph, nodeId);
      return {
        status: state.status,
        text: causeNodeId ? `Blocked by ${causeNodeId}` : "Blocked upstream",
        ...(causeNodeId ? { causeNodeId } : {}),
      };
    }
    case "Unready": {
      // Unready is never an error (engine semantics): either this node's own
      // required input/param is unset, or it cascades from an upstream node
      // that is itself unready. Only the latter names a cause.
      const causeNodeId = upstreamCause(graph, nodeId);
      return {
        status: state.status,
        text: causeNodeId ? `Waiting on ${causeNodeId}` : "Needs setup",
        ...(causeNodeId ? { causeNodeId } : {}),
      };
    }
    case "Error": {
      const causeNodeId = upstreamCause(graph, nodeId);
      const own = state.error ?? "Error";
      return {
        status: state.status,
        text: causeNodeId ? `${own} (from ${causeNodeId})` : own,
        ...(causeNodeId ? { causeNodeId } : {}),
      };
    }
  }
}
