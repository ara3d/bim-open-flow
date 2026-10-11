// Source nodes (TKT-138): which nodes the sources rail holds, how a wire out of
// one reads as a chip, and which source already holds a path. Pure and
// gratify-free. A source is an ordinary node in the document; this module only
// decides how the canvas draws it.

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";

type Document = State["document"];
type Catalog = ReadonlyMap<string, NodeDescriptor>;

/** How the canvas draws source nodes and the wires leaving them. */
export interface SourceView {
  /** Source nodes stand in a rail left of the other nodes, whatever layout they saved. */
  readonly rail: boolean;
  /** A parameter wired to a source shows a chip with the source's name instead of a wire. */
  readonly chips: boolean;
}

export const DEFAULT_SOURCE_VIEW: SourceView = { rail: true, chips: true };
export const NO_SOURCE_VIEW: SourceView = { rail: false, chips: false };

/** The source a chip names. */
export interface SourceChip {
  readonly sourceId: string;
  /** What the chip shows: the node's name. The document holds no node label, so it is the id. */
  readonly name: string;
}

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

/** Chips by target node and parameter: one for each edge from a source in
 *  `sources` into a node's parameter socket (an edge into a real input port is
 *  never a chip; a source has no Table output to feed one). */
export function sourceChips(
  document: Document,
  catalog: Catalog,
  sources: ReadonlySet<string>,
): ReadonlyMap<string, Readonly<Record<string, SourceChip>>> {
  const out = new Map<string, Record<string, SourceChip>>();
  for (const edge of document.structure.edges) {
    const from = splitEndpoint(edge.from);
    const to = splitEndpoint(edge.to);
    if (!sources.has(from.nodeId)) continue;
    const kind = document.structure.nodes.find((n) => n.id === to.nodeId)?.kind;
    if (!catalog.get(kind ?? "")?.params.some((p) => p.name === to.port && p.bindable)) continue;
    const byParam = out.get(to.nodeId) ?? {};
    byParam[to.port] = { sourceId: from.nodeId, name: from.nodeId };
    out.set(to.nodeId, byParam);
  }
  return out;
}

/** The nodes that use source `sourceId`: the targets of its edges. */
export function sourceUsers(document: Document, sourceId: string): string[] {
  const users = document.structure.edges
    .filter((e) => splitEndpoint(e.from).nodeId === sourceId)
    .map((e) => splitEndpoint(e.to).nodeId);
  return [...new Set(users)];
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
