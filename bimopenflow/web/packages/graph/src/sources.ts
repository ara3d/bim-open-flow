// Source nodes (TKT-138): which nodes the sources rail holds and which source
// already holds a path. Pure and gratify-free. A source is an ordinary node in
// the document; this module only decides how the canvas draws it. Its wires
// are drawn like any other (chips, a pill naming the source in place of a
// wire, were dropped on 2026-10-12: a second kind of connection with no
// affordance read as "unconnected").

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";

type Document = State["document"];
type Catalog = ReadonlyMap<string, NodeDescriptor>;

/** How the canvas draws source nodes. */
export interface SourceView {
  /** Unwired source nodes stand in a rail left of the other nodes, whatever
   *  layout they saved; a wired one sits in the flow beside its users. */
  readonly rail: boolean;
}

export const DEFAULT_SOURCE_VIEW: SourceView = { rail: true };
export const NO_SOURCE_VIEW: SourceView = { rail: false };

/** A source node has no inputs, at least one output, and no Table output:
 *  it holds a resource or a constant, not data. */
export function isSourceDescriptor(desc: NodeDescriptor | undefined): boolean {
  return desc !== undefined && desc.inputs.length === 0 && desc.outputs.length > 0
    && desc.outputs.every((o) => o.type !== "Table");
}

/** Ids of the source nodes of `document`, in document order. */
export function sourceNodeIds(document: Document, catalog: Catalog): Set<string> {
  return new Set(document.structure.nodes.filter((n) => isSourceDescriptor(catalog.get(n.kind))).map((n) => n.id));
}

const splitEndpoint = (endpoint: string): { nodeId: string; port: string } => {
  const dot = endpoint.indexOf(".");
  return { nodeId: endpoint.slice(0, dot), port: endpoint.slice(dot + 1) };
};

/** The sources of `sources` that stand in the rail: those with no wire. A
 *  wired source stays in the flow, where the layout places it beside its
 *  users (TKT-286): in the rail its wire crossed the graph. */
export function railSources(document: Document, sources: ReadonlySet<string>): Set<string> {
  const wired = new Set(document.structure.edges.map((e) => splitEndpoint(e.from).nodeId));
  return new Set([...sources].filter((id) => !wired.has(id)));
}

/** Compares paths the way the host does: slashes alike. */
export const samePath = (a: string, b: string): boolean => a.trim() !== "" && slashes(a) === slashes(b);

const slashes = (path: string): string => path.trim().split("\\").join("/");

/** The path a source node holds: its stored `path`, else the catalog default. */
export function sourcePath(document: Document, catalog: Catalog, nodeId: string): string {
  const node = document.structure.nodes.find((n) => n.id === nodeId);
  return document.values[nodeId]?.["path"]
    ?? catalog.get(node?.kind ?? "")?.params.find((p) => p.name === "path")?.default
    ?? "";
}

/** A source output that can feed a socket of type `type`. */
export interface SourceMatch {
  readonly id: string;
  /** "nodeId.port", the edge's `from`. */
  readonly endpoint: string;
}

/** The first source node holding `path` whose output has type `type`, other
 *  than `except` (the node asking: a source is never offered itself, TKT-217). */
export function findSourceByPath(
  document: Document,
  catalog: Catalog,
  path: string,
  type: PortType,
  except?: string,
): SourceMatch | null {
  for (const nodeId of sourceNodeIds(document, catalog)) {
    if (nodeId === except) continue;
    const node = document.structure.nodes.find((n) => n.id === nodeId)!;
    const output = catalog.get(node.kind)!.outputs.find((o) => o.type === type);
    if (output && samePath(sourcePath(document, catalog, nodeId), path)) return { id: nodeId, endpoint: `${nodeId}.${output.name}` };
  }
  return null;
}

/** The catalog kind that makes a source of `type` holding a path: a source
 *  with one output of that type and a FilePath parameter named `path`. */
export function pathSourceKind(catalog: Catalog, type: PortType): NodeDescriptor | undefined {
  return [...catalog.values()].find((d) =>
    isSourceDescriptor(d) && d.outputs.length === 1 && d.outputs[0]!.type === type
    && d.params.some((p) => p.name === "path" && p.kind === "FilePath"));
}

/** The source the path box of parameter `param` on `nodeId` offers to link to
 *  ("use <source>"): one holding `value`, of the type the parameter's socket
 *  takes, and never `nodeId` itself (TKT-217). Null for a parameter with no socket. */
export function sourceOffer(
  document: Document,
  catalog: Catalog,
  nodeId: string,
  param: string,
  value: string,
): SourceMatch | null {
  const kind = document.structure.nodes.find((n) => n.id === nodeId)?.kind ?? "";
  const type = catalog.get(kind)?.params.find((p) => p.name === param)?.bindable;
  return type ? findSourceByPath(document, catalog, value, type, nodeId) : null;
}
