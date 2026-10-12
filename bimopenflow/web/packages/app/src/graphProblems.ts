// The flow's problem list: every node that is not Ok, root causes first.
// Pure and DOM-free (the pattern of nodeBadge.ts); problemsPanel.ts paints it.

import type { NodeStatus } from "@bimopenflow/contracts";
import { schemaMessages, type State } from "@bimopenflow/state";
import { nodeTitle } from "@bimopenflow/graph";
import { nodeBadge } from "@bimopenflow/graph";

export interface Problem {
  readonly nodeId: string;
  /** nodeTitle(kind). */
  readonly title: string;
  /** "Warning" is a message from the static schema pass (TKT-209): it can sit
   *  on a node whose evaluation status is Ok, and never blocks anything. */
  readonly status: Exclude<NodeStatus, "Ok"> | "Warning";
  /** The nodeBadge text, or the schema pass's message for a Warning. */
  readonly text: string;
  readonly causeNodeId?: string;
  /** Edge count of the longest upstream chain; the primary sort key. */
  readonly depth: number;
}

const STATUS_ORDER: Readonly<Record<Problem["status"], number>> = {
  Error: 0,
  EffectPending: 1,
  Unavailable: 2,
  Unready: 3,
  Warning: 4,
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
 *  plus one Warning entry per schema message on any node, root causes first:
 *  by depth, then Error before EffectPending before Unavailable before
 *  Unready before Warning, then id. */
export function graphProblems(state: State): Problem[] {
  const { nodes, edges } = state.document.structure;
  const graph = { edges, evalState: state.evalState };
  const depths = upstreamDepths(nodes.map((n) => n.id), edges);
  const problems: Problem[] = [];
  for (const node of nodes) {
    const badge = nodeBadge(graph, node.id);
    const title = nodeTitle(node.kind);
    const depth = depths.get(node.id) ?? 0;
    if (badge && badge.status !== "Ok")
      problems.push({
        nodeId: node.id,
        title,
        status: badge.status,
        text: badge.text,
        ...(badge.causeNodeId ? { causeNodeId: badge.causeNodeId } : {}),
        depth,
      });
    for (const text of schemaMessages(state, node.id))
      problems.push({ nodeId: node.id, title, status: "Warning", text, depth });
  }
  return problems.sort(
    (a, b) =>
      a.depth - b.depth ||
      STATUS_ORDER[a.status] - STATUS_ORDER[b.status] ||
      (a.nodeId < b.nodeId ? -1 : a.nodeId > b.nodeId ? 1 : 0) ||
      (a.text < b.text ? -1 : a.text > b.text ? 1 : 0),
  );
}

/** "" | "1 problem" | "3 problems · 1 error" | "3 problems · 1 error · 2 warnings" | "2 warnings".
 *  Schema warnings are counted apart from problems; the error count appears only when non-zero. */
export function problemsSummary(problems: readonly Problem[]): string {
  const warnings = problems.filter((p) => p.status === "Warning").length;
  const real = problems.length - warnings;
  const parts: string[] = [];
  if (real > 0) {
    parts.push(`${real} ${real === 1 ? "problem" : "problems"}`);
    const errors = problems.filter((p) => p.status === "Error").length;
    if (errors > 0) parts.push(`${errors} ${errors === 1 ? "error" : "errors"}`);
  }
  if (warnings > 0) parts.push(`${warnings} ${warnings === 1 ? "warning" : "warnings"}`);
  return parts.join(" · ");
}
