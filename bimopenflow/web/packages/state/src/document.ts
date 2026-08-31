// Client-side mirror of the frozen four-layer graph document format
// (spec/dataflow-graph/format, canonical implementation: src/Ara3D.NodeGraph).
// The client round-trips the JSON it received and applies edits structurally;
// full canonical byte-identity (hashing) lives server-side.

export interface GraphNode {
  readonly id: string;
  readonly kind: string;
  readonly version: number;
}

export interface GraphEdge {
  readonly from: string;
  readonly to: string;
}

export interface NodeLayout {
  readonly x: number;
  readonly y: number;
  readonly w?: number;
  readonly h?: number;
}

export interface GraphStructure {
  readonly nodes: readonly GraphNode[];
  readonly edges: readonly GraphEdge[];
}

export interface GraphDocument {
  readonly formatVersion: string;
  readonly structure: GraphStructure;
  readonly values: Readonly<Record<string, Readonly<Record<string, string>>>>;
  readonly layout: Readonly<Record<string, NodeLayout>>;
  readonly session?: unknown;
}

export const emptyDocument: GraphDocument = {
  formatVersion: "0.1.0",
  structure: { nodes: [], edges: [] },
  values: {},
  layout: {},
};

export interface PortRef {
  readonly nodeId: string;
  readonly port: string;
}

/** Node ids contain no dot, so the first dot splits "nodeId.port" unambiguously. */
export function parsePortRef(endpoint: string): PortRef {
  const i = endpoint.indexOf(".");
  if (i <= 0 || i >= endpoint.length - 1)
    throw new Error(`Invalid edge endpoint '${endpoint}': expected 'nodeId.port'`);
  return { nodeId: endpoint.slice(0, i), port: endpoint.slice(i + 1) };
}

function fail(message: string): never {
  throw new Error(message);
}

function isObject(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === "object" && !Array.isArray(v);
}

function readNode(v: unknown): GraphNode {
  if (!isObject(v) || typeof v.id !== "string" || typeof v.kind !== "string" || typeof v.version !== "number")
    fail("Each node must be {id, kind, version}");
  return { id: v.id, kind: v.kind, version: v.version };
}

function readEdge(v: unknown): GraphEdge {
  if (!isObject(v) || typeof v.from !== "string" || typeof v.to !== "string")
    fail("Each edge must be {from, to}");
  parsePortRef(v.from);
  parsePortRef(v.to);
  return { from: v.from, to: v.to };
}

function readValues(v: unknown): Record<string, Record<string, string>> {
  if (!isObject(v)) fail("'values' must be an object");
  const result: Record<string, Record<string, string>> = {};
  for (const [nodeId, params] of Object.entries(v)) {
    if (!isObject(params)) fail(`values['${nodeId}'] must be an object`);
    const p: Record<string, string> = {};
    for (const [name, value] of Object.entries(params)) {
      if (typeof value !== "string")
        fail(`Parameter '${nodeId}.${name}' must be a string (canonical string form)`);
      p[name] = value;
    }
    result[nodeId] = p;
  }
  return result;
}

function readLayout(v: unknown): Record<string, NodeLayout> {
  if (!isObject(v)) fail("'layout' must be an object");
  const result: Record<string, NodeLayout> = {};
  for (const [nodeId, l] of Object.entries(v)) {
    if (!isObject(l) || typeof l.x !== "number" || typeof l.y !== "number")
      fail(`Layout for '${nodeId}' must contain numeric 'x' and 'y'`);
    result[nodeId] = {
      x: l.x,
      y: l.y,
      ...(typeof l.w === "number" ? { w: l.w } : {}),
      ...(typeof l.h === "number" ? { h: l.h } : {}),
    };
  }
  return result;
}

/** Parses a graph document JSON string, validating shape (not catalog conformance). */
export function parseDocument(json: string): GraphDocument {
  const root: unknown = JSON.parse(json);
  if (!isObject(root)) fail("Graph document must be a JSON object");
  for (const key of Object.keys(root))
    if (!["formatVersion", "structure", "values", "layout", "session"].includes(key))
      fail(`Unknown top-level member '${key}'`);
  const structure = root.structure ?? fail("Missing required 'structure' layer");
  if (root.values === undefined) fail("Missing required 'values' layer");
  if (!isObject(structure) || !Array.isArray(structure.nodes) || !Array.isArray(structure.edges))
    fail("'structure' must contain 'nodes' and 'edges' arrays");
  return {
    formatVersion: typeof root.formatVersion === "string" ? root.formatVersion : "0.1.0",
    structure: {
      nodes: structure.nodes.map(readNode),
      edges: structure.edges.map(readEdge),
    },
    values: readValues(root.values),
    layout: root.layout === undefined ? {} : readLayout(root.layout),
    ...(root.session !== undefined ? { session: root.session } : {}),
  };
}

function sortKeysDeep(v: unknown): unknown {
  if (Array.isArray(v)) return v.map(sortKeysDeep);
  if (isObject(v))
    return Object.fromEntries(
      Object.keys(v).sort().map((k) => [k, sortKeysDeep(v[k])]));
  return v;
}

/**
 * Serializes toward the canonical form (sorted keys at every level, nodes
 * sorted by id, edges by 'to', 2-space indent, LF, one trailing LF; empty
 * layout and session omitted). Byte-identity with the server's canonical
 * writer is NOT guaranteed for all doubles — the server re-canonicalizes.
 */
export function serializeDocument(doc: GraphDocument): string {
  const plain: Record<string, unknown> = {
    formatVersion: doc.formatVersion,
    structure: {
      nodes: [...doc.structure.nodes].sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)),
      edges: [...doc.structure.edges].sort((a, b) => (a.to < b.to ? -1 : a.to > b.to ? 1 : 0)),
    },
    values: doc.values,
  };
  if (Object.keys(doc.layout).length > 0) plain.layout = doc.layout;
  if (doc.session !== undefined && !(isObject(doc.session) && Object.keys(doc.session).length === 0))
    plain.session = doc.session;
  return JSON.stringify(sortKeysDeep(plain), null, 2) + "\n";
}
