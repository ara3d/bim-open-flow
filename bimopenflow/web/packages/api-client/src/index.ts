// Generated from contracts/contracts.json v0.1.0 by contracts/generate.mjs.
// Do not edit by hand.
import type { ModelSummary, AnalysisSummary, AnalysisVersion, NodeCatalog, EvalUpdate, TableSlice, SuggestionList, RunSummary } from "@bimopenflow/contracts";

export interface ApiClientOptions {
  baseUrl?: string;
  fetch?: typeof fetch;
}

export class ApiClient {
  private readonly baseUrl: string;
  private readonly fetchFn: typeof fetch;

  constructor(options: ApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? "").replace(/\/$/, "");
    this.fetchFn = options.fetch ?? fetch.bind(globalThis);
  }

  private async request(method: string, path: string, query?: Record<string, unknown>, body?: string): Promise<Response> {
    let url = this.baseUrl + path;
    if (query) {
      const q = Object.entries(query).filter(([, v]) => v !== undefined && v !== null)
        .map(([k, v]) => k + "=" + encodeURIComponent(String(v))).join("&");
      if (q) url += "?" + q;
    }
    const res = await this.fetchFn(url, { method, body, headers: body !== undefined ? { "content-type": "application/json" } : undefined });
    if (!res.ok) throw new Error(method + " " + path + " -> " + res.status + ": " + await res.text());
    return res;
  }

  async listModels(): Promise<ModelSummary[]> {
    const res = await this.request("GET", `/api/models`, undefined, undefined);
    return res.json() as Promise<ModelSummary[]>;
  }

  async listAnalyses(): Promise<AnalysisSummary[]> {
    const res = await this.request("GET", `/api/analyses`, undefined, undefined);
    return res.json() as Promise<AnalysisSummary[]>;
  }

  async getAnalysis(id: string): Promise<string> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}`, undefined, undefined);
    return res.text();
  }

  async putAnalysis(id: string, body: string): Promise<AnalysisSummary> {
    const res = await this.request("PUT", `/api/analyses/${encodeURIComponent(id)}`, undefined, body);
    return res.json() as Promise<AnalysisSummary>;
  }

  async getAnalysisHistory(id: string): Promise<AnalysisVersion[]> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/history`, undefined, undefined);
    return res.json() as Promise<AnalysisVersion[]>;
  }

  async getNodeCatalog(): Promise<NodeCatalog> {
    const res = await this.request("GET", `/api/catalog/nodes`, undefined, undefined);
    return res.json() as Promise<NodeCatalog>;
  }

  async getAnalysisState(id: string): Promise<EvalUpdate> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/state`, undefined, undefined);
    return res.json() as Promise<EvalUpdate>;
  }

  async getResult(id: string, nodeId: string, port: string, skip?: number, take?: number): Promise<TableSlice> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/results/${encodeURIComponent(nodeId)}/${encodeURIComponent(port)}`, { skip, take }, undefined);
    return res.json() as Promise<TableSlice>;
  }

  async getSuggestions(id: string, nodeId: string, param: string): Promise<SuggestionList> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/suggestions/${encodeURIComponent(nodeId)}/${encodeURIComponent(param)}`, undefined, undefined);
    return res.json() as Promise<SuggestionList>;
  }

  async listRuns(id: string): Promise<RunSummary[]> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/runs`, undefined, undefined);
    return res.json() as Promise<RunSummary[]>;
  }

  async createRun(id: string): Promise<RunSummary> {
    const res = await this.request("POST", `/api/analyses/${encodeURIComponent(id)}/runs`, undefined, undefined);
    return res.json() as Promise<RunSummary>;
  }

  async getRun(id: string, fileName: string): Promise<string> {
    const res = await this.request("GET", `/api/analyses/${encodeURIComponent(id)}/runs/${encodeURIComponent(fileName)}`, undefined, undefined);
    return res.text();
  }

  /** Server-sent events stream of EvalUpdate. Returns an unsubscribe function. */
  analysisEvents(id: string, onEvent: (e: EvalUpdate) => void, onError?: (err: unknown) => void): () => void {
    const source = new EventSource(this.baseUrl + `/api/analyses/${encodeURIComponent(id)}/events`);
    source.onmessage = (m) => onEvent(JSON.parse(m.data) as EvalUpdate);
    if (onError) source.onerror = onError;
    return () => source.close();
  }

}
