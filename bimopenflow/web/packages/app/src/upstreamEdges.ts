// The animated-path computation for TKT-24: which wires feed a selected
// node, upstream to the sources. Pure and gratify-free so it is testable
// without a canvas; canvasParts.ts uses the result to decide which wires
// paint the travelling-flow highlight.

/** The slice of CanvasEdge that the traversal needs. */
export interface FlowEdge {
  readonly id: string;
  readonly from: string; // "nodeId.port"
  readonly to: string;
}

const nodeIdOf = (portRef: string): string => portRef.split(".")[0]!;

/**
 * Edge ids on every path that feeds `nodeId`, walking upstream to the
 * sources (nodes with no inputs). Handles diamonds (an edge reached from two
 * branches is included once) and cycles (a visited-node guard stops the
 * walk from looping forever). Returns an empty set when `nodeId` is null or
 * has no upstream edges.
 */
export function upstreamEdges(
  model: { readonly edges: readonly FlowEdge[] },
  nodeId: string | null,
): ReadonlySet<string> {
  if (nodeId === null) return new Set();
  const upstreamNodeIds = new Set<string>([nodeId]);
  const pending = [nodeId];
  while (pending.length > 0) {
    const id = pending.pop()!;
    for (const edge of model.edges) {
      if (nodeIdOf(edge.to) !== id) continue;
      const fromId = nodeIdOf(edge.from);
      if (!upstreamNodeIds.has(fromId)) {
        upstreamNodeIds.add(fromId);
        pending.push(fromId);
      }
    }
  }
  const edgeIds = new Set<string>();
  for (const edge of model.edges)
    if (upstreamNodeIds.has(nodeIdOf(edge.from)) && upstreamNodeIds.has(nodeIdOf(edge.to)))
      edgeIds.add(edge.id);
  return edgeIds;
}
