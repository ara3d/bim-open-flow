import type { EvalUpdate } from "@bimopenflow/contracts";
import { serializeDocument } from "./document.js";
import type { Store } from "./store.js";

/**
 * The slice of ApiClient that connectAnalysis needs. Structural, so tests can
 * pass a plain fake object; a real ApiClient instance satisfies it as-is.
 */
export interface AnalysisApi {
  getAnalysis(id: string): Promise<string>;
  putAnalysis(id: string, body: string): Promise<unknown>;
  getAnalysisState(id: string): Promise<EvalUpdate>;
  analysisEvents(
    id: string,
    onEvent: (e: EvalUpdate) => void,
    onError?: (err: unknown) => void,
  ): () => void;
}

export interface AnalysisConnection {
  /** Saves the current document via putAnalysis and clears the dirty flag. */
  save(): Promise<void>;
  /** Unsubscribes from the evaluation-update stream. */
  dispose(): void;
}

/**
 * Loads the analysis document and evaluation state into the store, then wires
 * the server's evaluation-update stream into applyServerState dispatches.
 */
export async function connectAnalysis(
  store: Store,
  api: AnalysisApi,
  analysisId: string,
): Promise<AnalysisConnection> {
  store.dispatch({ type: "setDocument", json: await api.getAnalysis(analysisId) });
  store.dispatch({ type: "applyServerState", update: await api.getAnalysisState(analysisId) });
  const unsubscribe = api.analysisEvents(analysisId, (update) =>
    store.dispatch({ type: "applyServerState", update }));
  return {
    save: async () => {
      await api.putAnalysis(analysisId, serializeDocument(store.getState().document));
      store.dispatch({ type: "markSaved" });
    },
    dispose: () => unsubscribe(),
  };
}
