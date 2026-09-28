// The flow's problem list: every node that is not Ok, root causes first.
// Pure and DOM-free (the pattern of nodeBadge.ts); problemsPanel.ts paints it.

import type { NodeStatus } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { nodeTitle } from "@bimopenflow/graph";
import { nodeBadge } from "@bimopenflow/graph";

export interface Problem {
  readonly nodeId: string;
  /** nodeTitle(kind). */
  readonly title: string;
  readonly status: Exclude<NodeStatus, "Ok">;
  /** The nodeBadge text. */
  readonly text: string;
  readonly causeNodeId?: string;
  /** Edge count of the longest upstream chain; the primary sort key. */
  readonly depth: number;
}

const STATUS_ORDER: Readonly<Record<Exclude<NodeStatus, "Ok">, number>> = {
  Error: 0,
  EffectPending: 1,
  Unavailable: 2,
  Unready: 3,
};

const nodeIdOf = (portRef: string): string => portRef.split(".")[0]!;

/** Longest upstream chain per node id. A cycle (which documents disallow)
 *  is cut where it closes, so the walk always ends. */
function upstreamDepths(
  nodeIds: readonly string[],
  edges: readonly { readonly from: string; readonly to: string }[],
): Map<string, number> {
  const parents = new Map<string, string[]>();
  for (const e of edges) {
    const to = nodeIdOf(e.to);
    const list = parents.get(to) ?? [];
    list.push(nodeIdOf(e.from));
    parents.set(to, list);
  }
  const memo = new Map<string, number>();
  const depthOf = (id: string, path: Set<string>): number => {
    const known = memo.get(id);
    if (known !== undefined) return known;
    path.add(id);
    let best = 0;
    for (const p of parents.get(id) ?? []) {
      if (path.has(p)) continue;
      best = Math.max(best, depthOf(p, path) + 1);
    }
    path.delete(id);
    memo.set(id, best);
    return best;
  };
  for (const id of nodeIds) depthOf(id, new Set());
  return memo;
}

/** Every node whose status is not Ok (nodes with no state yet are left out),
 *  root causes first: by depth, then Error before EffectPending before
 *  Unavailable before Unready, then id. */
export function graphProblems(state: State): Problem[] {
  const { nodes, edges } = state.document.structure;
  const graph = { edges, evalState: state.evalState };
  const depths = upstreamDepths(nodes.map((n) => n.id), edges);
  const problems: Problem[] = [];
  for (const node of nodes) {
    const badge = nodeBadge(graph, node.id);
    if (!badge || badge.status === "Ok") continue;
    problems.push({
      nodeId: node.id,
      title: nodeTitle(node.kind),
      status: badge.status,
      text: badge.text,
      ...(badge.causeNodeId ? { causeNodeId: badge.causeNodeId } : {}),
      depth: depths.get(node.id) ?? 0,
    });
  }
  return problems.sort(
    (a, b) =>
      a.depth - b.depth ||
      STATUS_ORDER[a.status] - STATUS_ORDER[b.status] ||
      (a.nodeId < b.nodeId ? -1 : a.nodeId > b.nodeId ? 1 : 0),
  );
}

/** "" | "1 problem" | "3 problems · 1 error" (the error count appears only when non-zero). */
export function problemsSummary(problems: readonly Problem[]): string {
  if (problems.length === 0) return "";
  const noun = problems.length === 1 ? "problem" : "problems";
  const errors = problems.filter((p) => p.status === "Error").length;
  const tail = errors === 0 ? "" : ` · ${errors} ${errors === 1 ? "error" : "errors"}`;
  return `${problems.length} ${noun}${tail}`;
}
