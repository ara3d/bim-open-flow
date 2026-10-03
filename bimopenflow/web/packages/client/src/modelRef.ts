// Mapping from a node to the model it renders: the model file path comes from
// a "path" param (own or nearest upstream), and the catalog id from matching
// that path against the host's model list.

import type { ModelSummary } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";

const nodeOf = (endpoint: string): string => endpoint.split(".", 1)[0]!;

const pathValue = (doc: GraphDocument, nodeId: string): string | undefined => {
  const value = doc.values[nodeId]?.["path"];
  return value && value.trim().length > 0 ? value : undefined;
};

/**
 * The model file path feeding a node: its own "path" param value when set,
 * else the nearest upstream node's (breadth-first over incoming edges), so
 * downstream view3d.* nodes inherit the model of the view3d.instances (or
 * other loader) they hang off.
 */
export function modelPathFor(
  doc: GraphDocument,
  nodeId: string,
): string | undefined {
  const visited = new Set<string>([nodeId]);
  let frontier = [nodeId];
  while (frontier.length > 0) {
    for (const id of frontier) {
      const path = pathValue(doc, id);
      if (path) return path;
    }
    frontier = doc.structure.edges
      .filter((e) => frontier.includes(nodeOf(e.to)))
      .map((e) => nodeOf(e.from))
      .filter((id) => !visited.has(id));
    frontier.forEach((id) => visited.add(id));
  }
  return undefined;
}

const normalize = (path: string): string =>
  path.replace(/\\/g, "/").toLowerCase();

/**
 * The catalog model whose sourcePath matches a node's path param: exact match
 * after separator/case normalization, else the unique model whose sourcePath
 * ends with the (root-relative) path.
 */
export function matchModelId(
  models: readonly ModelSummary[],
  path: string,
): string | undefined {
  const wanted = normalize(path);
  const exact = models.find((m) => normalize(m.sourcePath) === wanted);
  if (exact) return exact.id;
  const suffix = wanted.startsWith("/") ? wanted : `/${wanted}`;
  const bySuffix = models.filter((m) => normalize(m.sourcePath).endsWith(suffix));
  return bySuffix.length === 1 ? bySuffix[0]!.id : undefined;
}
