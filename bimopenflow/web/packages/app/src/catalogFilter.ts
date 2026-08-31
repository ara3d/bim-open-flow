import type { NodeDescriptor } from "@bimopenflow/contracts";

/**
 * Case-insensitive catalog filter: every whitespace-separated term must appear
 * in the node's kind or description. An empty query matches everything.
 */
export function filterCatalog(
  nodes: readonly NodeDescriptor[],
  query: string,
): NodeDescriptor[] {
  const terms = query.toLowerCase().split(/\s+/).filter((t) => t.length > 0);
  return nodes.filter((n) => {
    const haystack = (n.kind + " " + n.description).toLowerCase();
    return terms.every((t) => haystack.includes(t));
  });
}
