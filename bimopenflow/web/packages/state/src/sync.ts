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
  /** Unsubscribes from the evaluation-update stream and stops autosave. */
  dispose(): void;
}

export interface ConnectOptions {
  /** When set, document edits are auto-saved this many ms after the last
   *  change (coalesced: one PUT in flight at a time, latest document wins). */
  autosaveMs?: number;
  /** Called when an autosave PUT fails; autosave then waits for the next edit. */
  onSaveError?: (err: unknown) => void;
}

/**
 * Loads the analysis document and evaluation state into the store, then wires
 * the server's evaluation-update stream into applyServerState dispatches.
 */
export async function connectAnalysis(
  store: Store,
  api: AnalysisApi,
  analysisId: string,
  options: ConnectOptions = {},
): Promise<AnalysisConnection> {
  store.dispatch({ type: "setDocument", json: await api.getAnalysis(analysisId) });
  store.dispatch({ type: "applyServerState", update: await api.getAnalysisState(analysisId) });
  const unsubscribe = api.analysisEvents(analysisId, (update) =>
    store.dispatch({ type: "applyServerState", update }));

  // Saves the document as of call time; only clears dirty when no edit raced in.
  const saveNow = async () => {
    const doc = store.getState().document;
    await api.putAnalysis(analysisId, serializeDocument(doc));
    if (store.getState().document === doc) store.dispatch({ type: "markSaved" });
  };

  let timer: ReturnType<typeof setTimeout> | null = null;
  let inFlight = false;
  let disposed = false;

  const schedule = () => {
    if (timer !== null) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = null;
      void autosave();
    }, options.autosaveMs);
  };

  const autosave = async () => {
    if (inFlight || disposed) return;
    inFlight = true;
    try {
      await saveNow();
      if (!disposed && store.getState().dirty) schedule(); // edits raced the PUT
    } catch (e) {
      options.onSaveError?.(e);
    } finally {
      inFlight = false;
    }
  };

  // Debounce on document change only, so streamed eval updates never reset it.
  let lastDoc = store.getState().document;
  const unsubscribeStore =
    options.autosaveMs === undefined
      ? undefined
      : store.subscribe(() => {
          const state = store.getState();
          if (state.document === lastDoc) return;
          lastDoc = state.document;
          if (state.dirty) schedule();
        });

  return {
    save: saveNow,
    dispose: () => {
      disposed = true;
      if (timer !== null) clearTimeout(timer);
      unsubscribeStore?.();
      unsubscribe();
    },
  };
}
