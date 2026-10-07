// The API of the GitHub Pages page: no host stands behind it, so a fetch-shaped
// function answers the routes the editor (@bimopenflow/app) calls on load and
// while browsing, from site/data/buildings.json and the node catalog. The data
// file is written by the test PublicBuildingsSiteData.Write
// (tests/flow/BimOpenFlow.TableWorkflows.Tests): each graph's document, and per
// node its status and, for a table output, the row count, columns, and first
// rows. Nothing is evaluated here. A PUT (autosave, the session, a new flow) is
// kept in memory until the page reloads and re-evaluates nothing; a Run, a
// history, or a model request fails with NO_HOST.

import { ApiClient } from "@bimopenflow/api-client";
import type {
  AnalysisSummary, ColumnSchema, ColumnType, EditorSession, EvalUpdate, NodeCatalog,
  NodeStatus, SuggestionList, TableSlice,
} from "@bimopenflow/contracts";

/** One node of a graph in buildings.json. Column types are .NET type names (String, Int64). */
export interface SiteNode {
  status: NodeStatus;
  error?: string;
  rowCount?: number;
  columns?: { name: string; type: string }[];
  rows?: unknown[][];
}

export interface SiteGraph {
  id: string;
  building: string;
  buildingTitle: string;
  question: string;
  /** The graph document as committed in samples/buildings. */
  document: unknown;
  nodes: Record<string, SiteNode>;
}

/** site/data/buildings.json. */
export interface SiteData {
  about: string;
  /** How many rows of each table the file keeps. */
  sampleRows: number;
  graphs: SiteGraph[];
}

/** The one sentence the page and every refused request say. */
export const RESULTS_NOTE =
  "Results were computed by the tests; edit a graph locally in the editor to re-run it.";

/** What a request the page cannot answer fails with. */
export const NO_HOST = `This page has no host. ${RESULTS_NOTE}`;

/** The output port buildings.json records for every node with a table. */
export const RECORDED_PORT = "table";

const COLUMN_TYPES: Record<string, ColumnType> = {
  String: "Text", Boolean: "Boolean",
  Byte: "Integer", Int16: "Integer", Int32: "Integer", Int64: "Integer",
  Single: "Number", Double: "Number", Decimal: "Number",
};

/** A .NET column type name as the contract's column type; Text when unknown. */
export const columnType = (dotNet: string): ColumnType => COLUMN_TYPES[dotNet] ?? "Text";

const columnsOf = (node: SiteNode): ColumnSchema[] =>
  (node.columns ?? []).map((c) => ({ name: c.name, type: columnType(c.type) }));

interface Edge { from: string; to: string }
interface Doc { structure?: { nodes?: { id: string; kind: string }[]; edges?: Edge[] } }

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
const text = (body: string, status = 200): Response =>
  new Response(body, { status, headers: { "content-type": "text/plain" } });
const refused = (): Response => text(NO_HOST, 404);

/**
 * A fetch over `data` and `catalog` for an ApiClient with baseUrl "". Only the
 * path and query are read, so "/api/analyses" and an absolute URL both work.
 */
export function staticFetch(data: SiteData, catalog: NodeCatalog): typeof fetch {
  const graphs = new Map(data.graphs.map((g) => [g.id, g]));
  /** Documents as last PUT, or as recorded; a PUT never changes results. */
  const documents = new Map(data.graphs.map((g) => [g.id, JSON.stringify(g.document)]));
  let session: EditorSession = { selection: [] };
  const kinds = new Map(catalog.nodes.map((n) => [n.kind, n]));

  const summaries = (): AnalysisSummary[] => [...documents.keys()].map((id) => ({ id, graphHash: "" }));

  const state = (id: string): EvalUpdate => ({
    analysisId: id,
    nodes: Object.entries(graphs.get(id)?.nodes ?? {}).map(([nodeId, n]) => ({
      nodeId, status: n.status, warnings: [], ...(n.error ? { error: n.error } : {}),
    })),
  });

  const result = (id: string, nodeId: string, port: string, query: URLSearchParams): Response => {
    const node = graphs.get(id)?.nodes[nodeId];
    if (port !== RECORDED_PORT || !node?.columns)
      return text(`No result for ${nodeId}.${port} is recorded on this page.`, 404);
    const rows = node.rows ?? [];
    const skip = Math.max(0, Number(query.get("skip") ?? 0) || 0);
    const take = query.has("take") ? Math.max(0, Number(query.get("take")) || 0) : rows.length;
    const slice: TableSlice = {
      columns: columnsOf(node),
      rows: rows.slice(skip, skip + take),
      totalRows: node.rowCount ?? rows.length,
      skip,
    };
    return json(slice);
  };

  /** A ColumnsOfInput suggestion lists the recorded columns of the node wired into that input. */
  const suggestions = (id: string, nodeId: string, param: string): SuggestionList => {
    const graph = graphs.get(id);
    const doc = JSON.parse(documents.get(id) ?? "{}") as Doc;
    const kind = doc.structure?.nodes?.find((n) => n.id === nodeId)?.kind;
    const suggest = kind ? kinds.get(kind)?.params.find((p) => p.name === param)?.suggest : undefined;
    if (!graph || suggest?.kind !== "ColumnsOfInput")
      return { status: "Unavailable", values: [], reason: NO_HOST };
    const edge = doc.structure?.edges?.find((e) => e.to === `${nodeId}.${suggest.source}`);
    const upstream = edge ? graph.nodes[edge.from.split(".")[0]!] : undefined;
    if (!upstream?.columns) return { status: "Unready", values: [], reason: "Nothing recorded is wired into this input." };
    return { status: "Ok", values: columnsOf(upstream).map((c) => ({ value: c.name, detail: c.type })) };
  };

  return async (input, init) => {
    const href = typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
    const url = new URL(href, "http://static.invalid");
    const method = (init?.method ?? "GET").toUpperCase();
    const parts = url.pathname.split("/").filter(Boolean).map(decodeURIComponent);
    if (parts[0] !== "api") return refused();
    const [, area, id, sub, ...rest] = parts;

    if (area === "models" && parts.length === 2 && method === "GET") return json([]);
    if (area === "catalog" && id === "nodes" && method === "GET") return json(catalog);
    if (area === "session") {
      if (method === "PUT") session = JSON.parse(String(init?.body ?? "{}")) as EditorSession;
      return json(session);
    }
    if (area !== "analyses") return refused();
    if (id === undefined) return method === "GET" ? json(summaries()) : refused();
    if (sub === undefined) {
      if (method === "PUT") {
        documents.set(id, String(init?.body ?? ""));
        return json({ id, graphHash: "" } satisfies AnalysisSummary);
      }
      const doc = documents.get(id);
      return method === "GET" && doc !== undefined ? text(doc) : refused();
    }
    if (method !== "GET" || !documents.has(id)) return refused();
    if (sub === "state") return json(state(id));
    if (sub === "results" && rest.length === 2) return result(id, rest[0]!, rest[1]!, url.searchParams);
    if (sub === "suggestions" && rest.length === 2) return json(suggestions(id, rest[0]!, rest[1]!));
    return refused();
  };
}

/** An ApiClient over the static fetch. Its evaluation stream never fires,
 *  since nothing on the page evaluates, so no EventSource is opened. */
export class StaticApiClient extends ApiClient {
  override analysisEvents(): () => void {
    return () => {};
  }
}
