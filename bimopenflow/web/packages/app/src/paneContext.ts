import type { TableSlice } from "@bimopenflow/contracts";
import type { PaneContext } from "@bimopenflow/panes";

/** The slice of ApiClient the pane context needs; structural for test fakes. */
export interface ResultApi {
  getResult(
    analysisId: string,
    nodeId: string,
    port: string,
    skip?: number,
    take?: number,
  ): Promise<TableSlice>;
}

export const DEFAULT_PAGE_SIZE = 200;

/**
 * PaneContext bound to one analysis: requestTable pages through the host's
 * result endpoint; resolveAsset maps graph asset URLs to fetchable ones.
 */
export function makePaneContext(api: ResultApi, analysisId: string): PaneContext {
  return {
    requestTable: (nodeId, port, skip = 0, take = DEFAULT_PAGE_SIZE) =>
      api.getResult(analysisId, nodeId, port, skip, take),
    // TODO: real model-url scheme once the host serves geometry. Assumed:
    // "model:{id}" resolves to /api/models/{id}/geometry.bos; anything else
    // passes through unchanged.
    resolveAsset: (url) =>
      url.startsWith("model:")
        ? `/api/models/${encodeURIComponent(url.slice("model:".length))}/geometry.bos`
        : url,
  };
}
