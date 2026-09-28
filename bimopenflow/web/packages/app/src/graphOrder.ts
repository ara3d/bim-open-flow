// The order in which a flow's nodes read as steps: sources first, each node
// after everything that feeds it. Pure and DOM-free, so the step list (and
// any other reading of the graph as a sequence) can share it.

import type { GraphDocument } from "@bimopenflow/state";

const nodeIdOf = (portRef: string): string => portRef.split(".")[0]!;

/** Distinct downstream node ids per node, in edge order. */
function downstreamMap(document: GraphDocument): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const node of document.structure.nodes) map.set(node.id, []);
  for (const edge of document.structure.edges) {
    const to = nodeIdOf(edge.to), list = map.get(nodeIdOf(edge.from));
    if (list && map.has(to) && !list.includes(to)) list.push(to);
  }
  return map;
}

/** Distinct upstream node ids per node, without the edges that close a cycle.
 *  A walk downstream from each node in document order finds the edge that
 *  returns to a node still on the walk; dropping it breaks the cycle at the
 *  first of its nodes in the document. */
function acyclicUpstream(document: GraphDocument): Map<string, string[]> {
  const downstream = downstreamMap(document);
  const upstream = new Map<string, string[]>();
  for (const id of downstream.keys()) upstream.set(id, []);
  const done = new Set<string>(), onWalk = new Set<string>();
  const walk = (id: string): void => {
    onWalk.add(id);
    for (const to of downstream.get(id)!) {
      if (onWalk.has(to)) continue;
      upstream.get(to)!.push(id);
      if (!done.has(to)) walk(to);
    }
    onWalk.delete(id);
    done.add(id);
  };
  for (const id of downstream.keys()) if (!done.has(id)) walk(id);
  return upstream;
}

/** Length of the longest upstream chain per node, over an acyclic map. */
function depths(upstream: ReadonlyMap<string, readonly string[]>): Map<string, number> {
  const depth = new Map<string, number>();
  const visit = (id: string): number => {
    const known = depth.get(id);
    if (known !== undefined) return known;
    let d = 0;
    for (const from of upstream.get(id)!) d = Math.max(d, visit(from) + 1);
    depth.set(id, d);
    return d;
  };
  for (const id of upstream.keys()) visit(id);
  return depth;
}

// Nodes with no layout sort after laid-out nodes of the same depth.
const coordinate = (value: number | undefined): number => value ?? Number.POSITIVE_INFINITY;

function compareCoordinate(a: number, b: number): number {
  return a === b ? 0 : a < b ? -1 : 1;
}

/** Nodes in dataflow order: by longest upstream chain, then by layout y then x
 *  within a depth, then id. Every node appears once; a cycle breaks at its
 *  first node. */
export function dataflowOrder(document: GraphDocument): string[] {
  const depth = depths(acyclicUpstream(document));
  const layout = document.layout;
  return document.structure.nodes
    .map((n) => n.id)
    .sort(
      (a, b) =>
        depth.get(a)! - depth.get(b)! ||
        compareCoordinate(coordinate(layout[a]?.y), coordinate(layout[b]?.y)) ||
        compareCoordinate(coordinate(layout[a]?.x), coordinate(layout[b]?.x)) ||
        (a < b ? -1 : a > b ? 1 : 0),
    );
}

/** The ids of the nodes feeding `nodeId`, in dataflow order. */
export function feeders(document: GraphDocument, nodeId: string): string[] {
  const direct = new Set<string>();
  for (const edge of document.structure.edges)
    if (nodeIdOf(edge.to) === nodeId) direct.add(nodeIdOf(edge.from));
  return dataflowOrder(document).filter((id) => direct.has(id));
}
