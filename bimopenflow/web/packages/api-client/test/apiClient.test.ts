// Guards the generated client against contracts/contracts.json: every endpoint
// method issues its declared verb at its declared path, and a non-OK response
// throws with the method, path, status, and body.
import { readFileSync } from "node:fs";
import { describe, expect, it, vi } from "vitest";
import { ApiClient } from "../src/index.js";

interface Endpoint { name: string; method: string; path: string; query?: Record<string, string>; sse?: string; response: string }
const contracts = JSON.parse(readFileSync(new URL("../../../../../contracts/contracts.json", import.meta.url), "utf-8")) as { endpoints: Endpoint[] };
const endpoint = (name: string): Endpoint => contracts.endpoints.find((e) => e.name === name)!;

/** The path an endpoint yields when every `{param}` is filled from `args` in order. */
const fillPath = (path: string, args: string[]): string => {
  let i = 0;
  return path.replace(/\{\w+\}/g, () => encodeURIComponent(args[i++]!));
};

const ID = "an 1";
const BASE = "http://host.test";

/** One call per fetch-backed endpoint, keyed by contract name, with the path args used. */
const calls: Record<string, { args: string[]; invoke: (api: ApiClient) => Promise<unknown> }> = {
  listModels: { args: [], invoke: (api) => api.listModels() },
  listAnalyses: { args: [], invoke: (api) => api.listAnalyses() },
  getAnalysis: { args: [ID], invoke: (api) => api.getAnalysis(ID) },
  putAnalysis: { args: [ID], invoke: (api) => api.putAnalysis(ID, "{}") },
  getAnalysisHistory: { args: [ID], invoke: (api) => api.getAnalysisHistory(ID) },
  getNodeCatalog: { args: [], invoke: (api) => api.getNodeCatalog() },
  getAnalysisState: { args: [ID], invoke: (api) => api.getAnalysisState(ID) },
  getResult: { args: [ID, "n/1", "out"], invoke: (api) => api.getResult(ID, "n/1", "out") },
  getSuggestions: { args: [ID, "n/1", "col"], invoke: (api) => api.getSuggestions(ID, "n/1", "col") },
  listRuns: { args: [ID], invoke: (api) => api.listRuns(ID) },
  createRun: { args: [ID], invoke: (api) => api.createRun(ID) },
  getRun: { args: [ID, "run 1.json"], invoke: (api) => api.getRun(ID, "run 1.json") },
  getEntityProperties: {
    args: ["m 1", "1234"],
    invoke: (api) => api.getEntityProperties("m 1", "1234"),
  },
};

function clientWith(body: string, status = 200) {
  const fetchFn = vi.fn<typeof fetch>().mockImplementation(async () => new Response(body, { status }));
  return { api: new ApiClient({ baseUrl: BASE + "/", fetch: fetchFn }), fetchFn };
}

describe("ApiClient against contracts.json", () => {
  it("covers every fetch-backed endpoint", () => {
    const fetched = contracts.endpoints.filter((e) => !e.sse && e.response !== "bytes").map((e) => e.name);
    expect(Object.keys(calls).sort()).toEqual(fetched.sort());
  });

  for (const [name, { args, invoke }] of Object.entries(calls)) {
    it(`${name} issues ${endpoint(name).method} ${endpoint(name).path}`, async () => {
      const { api, fetchFn } = clientWith(endpoint(name).response === "text" ? "text" : "[]");
      await invoke(api);
      const [url, init] = fetchFn.mock.calls[0]!;
      expect(String(url)).toBe(BASE + fillPath(endpoint(name).path, args));
      expect(init?.method).toBe(endpoint(name).method);
    });
  }

  it("sends a body as JSON only for the endpoint that declares one", async () => {
    const { api, fetchFn } = clientWith("{}");
    await api.putAnalysis(ID, '{"a":1}');
    expect(fetchFn.mock.calls[0]![1]).toMatchObject({ body: '{"a":1}', headers: { "content-type": "application/json" } });
    await api.listModels();
    expect(fetchFn.mock.calls[1]![1]?.body).toBeUndefined();
  });

  it("appends only the query parameters that are set", async () => {
    const { api, fetchFn } = clientWith("{}");
    await api.getResult(ID, "n", "out", 10);
    expect(String(fetchFn.mock.calls[0]![0])).toMatch(/\?skip=10$/);
    await api.getResult(ID, "n", "out", undefined, 5);
    expect(String(fetchFn.mock.calls[1]![0])).toMatch(/\?take=5$/);
  });

  it("strips a trailing slash from the base url and builds the model bytes url", () => {
    const api = new ApiClient({ baseUrl: BASE + "/" });
    expect(api.getModelBosUrl("m 1")).toBe(BASE + fillPath(endpoint("getModelBos").path, ["m 1"]));
  });

  it("throws method, path, status, and body on a non-OK response", async () => {
    const { api } = clientWith('{"error":"missing"}', 404);
    await expect(api.getAnalysis(ID)).rejects.toThrow(`GET /api/analyses/${encodeURIComponent(ID)} -> 404: {"error":"missing"}`);
  });
});
