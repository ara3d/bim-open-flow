// The page's load sequence over site/data/buildings.json: what @bimopenflow/app
// asks for on boot and on opening each graph, answered without a host.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { nodeCatalog } from "../src/catalog";
import { NO_HOST, RECORDED_PORT, StaticApiClient, staticFetch, type SiteData } from "../src/staticApi";

const dataFile = fileURLToPath(new URL("../../../../../site/data/buildings.json", import.meta.url));
const data = JSON.parse(readFileSync(dataFile, "utf8")) as SiteData;
const api = () => new StaticApiClient({ baseUrl: "", fetch: staticFetch(data, nodeCatalog) });

describe("staticApi over site/data/buildings.json", () => {
  it("lists every graph and serves the catalog with every kind they use", async () => {
    const client = api();
    expect((await client.listAnalyses()).map((a) => a.id)).toEqual(data.graphs.map((g) => g.id));
    const kinds = new Set((await client.getNodeCatalog()).nodes.map((n) => n.kind));
    for (const graph of data.graphs)
      for (const node of (graph.document as { structure: { nodes: { kind: string }[] } }).structure.nodes)
        expect(kinds.has(node.kind), `${graph.id}: ${node.kind}`).toBe(true);
    expect(await client.listModels()).toEqual([]);
    expect(await client.getSession()).toEqual({ selection: [] });
  });

  it.each(data.graphs.map((g) => [g.id, g] as const))("opens %s with every node Ok and a page of each table", async (id, graph) => {
    const client = api();
    const doc = JSON.parse(await client.getAnalysis(id)) as { structure: { nodes: { id: string; kind: string }[] } };
    expect(doc).toEqual(graph.document);
    const state = await client.getAnalysisState(id);
    expect(state.analysisId).toBe(id);
    expect(state.nodes.map((n) => n.nodeId).sort()).toEqual(doc.structure.nodes.map((n) => n.id).sort());
    expect(state.nodes.every((n) => n.status === "Ok")).toBe(true);
    for (const node of doc.structure.nodes) {
      const slice = await client.getResult(id, node.id, RECORDED_PORT, 0, 5);
      const recorded = graph.nodes[node.id]!;
      expect(slice.totalRows).toBe(recorded.rowCount);
      expect(slice.rows).toEqual(recorded.rows!.slice(0, 5));
      expect(slice.columns.map((c) => c.name)).toEqual(recorded.columns!.map((c) => c.name));
      expect(slice.columns.every((c) => ["Text", "Integer", "Number", "Boolean"].includes(c.type))).toBe(true);
    }
    expect(doc.structure.nodes.some((n) => n.kind === "chart.bar")).toBe(true);
  });

  it("pages within the recorded rows", async () => {
    const graph = data.graphs[0]!;
    const [nodeId, node] = Object.entries(graph.nodes).find(([, n]) => (n.rows?.length ?? 0) > 10)!;
    const slice = await api().getResult(graph.id, nodeId, RECORDED_PORT, 10, 5);
    expect(slice.skip).toBe(10);
    expect(slice.rows).toEqual(node.rows!.slice(10, 15));
  });

  it("suggests the columns wired into a ColumnsOfInput parameter", async () => {
    const graph = data.graphs.find((g) => g.id === "schependomlaan-elements-by-category")!;
    const list = await api().getSuggestions(graph.id, "byCategory", "groupBy");
    expect(list.status).toBe("Ok");
    expect(list.values.map((v) => v.value)).toEqual(graph.nodes.placed!.columns!.map((c) => c.name));
  });

  it("keeps a PUT in memory and refuses a Run, the event stream aside", async () => {
    const client = api();
    const id = data.graphs[0]!.id;
    await client.putAnalysis(id, "{\"edited\":true}");
    expect(await client.getAnalysis(id)).toBe("{\"edited\":true}");
    await expect(client.createRun(id)).rejects.toThrow(NO_HOST);
    const ask = await staticFetch(data, nodeCatalog)("/api/ask/model");
    expect(ask.status).toBe(404);
    expect(typeof client.analysisEvents()).toBe("function");
  });
});
