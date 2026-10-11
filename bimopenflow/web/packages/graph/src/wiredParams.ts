// What a bindable parameter shows while an edge feeds it. Pure and
// gratify-free: the view model asks for the previews of one node and hands
// them to inlineParams.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { fileName } from "./paramText.js";

/** Preview text of the value wired into a parameter: the upstream node and
 *  port ("src.source"), and for a Database the file name of the path the
 *  upstream node holds ("src.source · model.duckdb"). */
export function wiredPreview(
  document: State["document"],
  catalog: ReadonlyMap<string, NodeDescriptor>,
  from: string,
  bindable: string,
): string {
  const nodeId = from.slice(0, from.indexOf("."));
  const node = document.structure.nodes.find((n) => n.id === nodeId);
  if (bindable !== "Database" || !node) return from;
  const path = document.values[nodeId]?.["path"]
    ?? catalog.get(node.kind)?.params.find((p) => p.name === "path")?.default
    ?? "";
  return path === "" ? from : `${from} · ${fileName(path)}`;
}

/** Previews, by parameter name, of the bindable parameters of node `nodeId`
 *  (described by `desc`) that an edge feeds. A parameter with no edge, and
 *  an edge into a parameter that is not bindable, are absent. */
export function wiredPreviews(
  document: State["document"],
  catalog: ReadonlyMap<string, NodeDescriptor>,
  nodeId: string,
  desc: NodeDescriptor | undefined,
): Record<string, string> {
  const out: Record<string, string> = {};
  for (const edge of document.structure.edges) {
    const dot = edge.to.indexOf(".");
    if (edge.to.slice(0, dot) !== nodeId) continue;
    const param = desc?.params.find((p) => p.name === edge.to.slice(dot + 1));
    if (param?.bindable) out[param.name] = wiredPreview(document, catalog, edge.from, param.bindable);
  }
  return out;
}
