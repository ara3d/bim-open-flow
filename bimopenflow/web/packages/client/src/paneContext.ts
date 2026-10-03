import type {
  EntityProperties,
  SuggestionList,
  TableSlice,
} from "@bimopenflow/contracts";
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
  getSuggestions(
    analysisId: string,
    nodeId: string,
    param: string,
  ): Promise<SuggestionList>;
  getModelBosUrl(id: string): string;
  getEntityProperties(id: string, localId: string): Promise<EntityProperties>;
}

export const DEFAULT_PAGE_SIZE = 200;

const MODEL_SCHEME = "model:";

/** The catalog id inside a "model:{id}" URL, or null for any other URL. */
export const modelIdOf = (url: string): string | null =>
  url.startsWith(MODEL_SCHEME) ? url.slice(MODEL_SCHEME.length) : null;

/**
 * PaneContext bound to one analysis: requestTable pages through the host's
 * result endpoint; resolveAsset maps graph asset URLs to fetchable ones;
 * requestEntityProperties asks the host for one entity of a catalog model.
 */
export function makePaneContext(api: ResultApi, analysisId: string): PaneContext {
  return {
    requestTable: (nodeId, port, skip = 0, take = DEFAULT_PAGE_SIZE) =>
      api.getResult(analysisId, nodeId, port, skip, take),
    requestSuggestions: (nodeId, param) =>
      api.getSuggestions(analysisId, nodeId, param),
    // "model:{id}" resolves to the host's model-bytes endpoint; anything
    // else passes through unchanged.
    resolveAsset: (url) => {
      const id = modelIdOf(url);
      return id === null ? url : api.getModelBosUrl(id);
    },
    // Only catalog models have an entity index to query.
    requestEntityProperties: (modelUrl, localId) => {
      const id = modelIdOf(modelUrl);
      return id === null
        ? Promise.reject(new Error(`No catalog model for ${modelUrl}`))
        : api.getEntityProperties(id, String(localId));
    },
  };
}
