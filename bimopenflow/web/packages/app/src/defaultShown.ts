// Which node the panes show when nothing is selected (TKT-46). Pure over the
// document, the evaluation state, and the catalog, so it is testable without
// a store or a canvas. app.ts calls this once, in place of the null it used
// to pass to paneArea.showNode whenever primaryNodeId was null.

import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";

const nodeIdOf = (portRef: string): string => portRef.split(".")[0]!;

/**
 * NodeDescriptor carries no "kind class" field (no `category`, despite the
 * ticket's wording — see contracts/src/index.ts), so the four classes are
 * derived from output ports and the same kind conventions paneChoice.ts uses
 * for pane selection (isView3DKind/isVerdictKind are not exported there, so
 * the check is repeated here rather than reaching into that module).
 *
 * 0 = viewer or sink: a 3D/verdict-style node, or one with no outputs at all
 *     (nothing downstream could consume it even if it had one).
 * 1 = materialized relation: a Table output, already paged by the host.
 * 2 = relation: a Relation output, run on request rather than stored.
 * 3 = source: neither — a scalar-only node, or one the catalog has no
 *     descriptor for.
 */
function kindRank(desc: NodeDescriptor | undefined): 0 | 1 | 2 | 3 {
  if (!desc) return 3;
  if (desc.outputs.length === 0) return 0;
  if (desc.kind.startsWith("view3d.") || desc.kind.startsWith("compliance.") || desc.kind.includes("verdict"))
    return 0;
  if (desc.outputs.some((p) => p.type === "Table")) return 1;
  if (desc.outputs.some((p) => p.type === "Relation")) return 2;
  return 3;
}

const hasResultRank = (state: NodeState | undefined): 0 | 1 => (state?.status === "Ok" ? 0 : 1);

/**
 * Longest upstream chain (edge count) feeding each node, for every node in
 * one pass: a memoized walk over the incoming-edge map, with a visiting
 * guard so a cycle returns 0 for the node that closes it instead of looping.
 * O(nodes + edges).
 */
function upstreamDepths(document: GraphDocument): Map<string, number> {
  const incoming = new Map<string, Set<string>>();
  for (const n of document.structure.nodes) incoming.set(n.id, new Set());
  for (const edge of document.structure.edges) {
    const targets = incoming.get(nodeIdOf(edge.to));
    if (targets) targets.add(nodeIdOf(edge.from));
  }
  const depth = new Map<string, number>();
  const visiting = new Set<string>();
  const depthOf = (id: string): number => {
    const cached = depth.get(id);
    if (cached !== undefined) return cached;
    if (visiting.has(id)) return 0; // cycle guard
    visiting.add(id);
    let best = 0;
    for (const source of incoming.get(id) ?? []) best = Math.max(best, 1 + depthOf(source));
    visiting.delete(id);
    depth.set(id, best);
    return best;
  };
  for (const n of document.structure.nodes) depthOf(n.id);
  return depth;
}

/**
 * The node the pane area shows when no node is selected: `lastShown` if it
 * still exists in the document; else the best terminal node (no outgoing
 * edge), ranked by has-a-result, kind class, upstream depth, position
 * (right then down), then document order. Null when the graph is empty or
 * has no terminal node.
 */
export function defaultShownNode(
  document: GraphDocument,
  evalState: Readonly<Record<string, NodeState>>,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  lastShown: string | null,
): string | null {
  if (lastShown !== null && document.structure.nodes.some((n) => n.id === lastShown)) return lastShown;

  const withOutgoing = new Set<string>();
  for (const edge of document.structure.edges) withOutgoing.add(nodeIdOf(edge.from));
  const terminals = document.structure.nodes.filter((n) => !withOutgoing.has(n.id));
  if (terminals.length === 0) return null;

  const depths = upstreamDepths(document);
  let best = terminals[0]!;
  let bestScore = scoreOf(best, 0);
  for (let i = 1; i < terminals.length; i++) {
    const score = scoreOf(terminals[i]!, i);
    if (compare(score, bestScore) < 0) {
      best = terminals[i]!;
      bestScore = score;
    }
  }
  return best.id;

  function scoreOf(node: { id: string; kind: string }, docIndex: number) {
    const layout = document.layout[node.id];
    return {
      hasResult: hasResultRank(evalState[node.id]),
      kind: kindRank(catalog.get(node.kind)),
      depth: -(depths.get(node.id) ?? 0),
      x: -(layout?.x ?? 0),
      y: -(layout?.y ?? 0),
      docIndex,
    };
  }

  function compare(a: ReturnType<typeof scoreOf>, b: ReturnType<typeof scoreOf>): number {
    return (
      a.hasResult - b.hasResult ||
      a.kind - b.kind ||
      a.depth - b.depth ||
      a.x - b.x ||
      a.y - b.y ||
      a.docIndex - b.docIndex
    );
  }
}
