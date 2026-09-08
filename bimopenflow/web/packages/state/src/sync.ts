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
  /** Saves through the shared PUT queue, including edits made while it is in flight. */
  save(): Promise<void>;
  /** Unsubscribes from the evaluation-update stream and stops autosave. */
  dispose(): void;
}

export interface ConnectOptions {
  /** When set, document edits are auto-saved this many ms after the last
   *  change (coalesced: one PUT in flight at a time, latest document wins). */
  autosaveMs?: number;
  /** Called when an autosave PUT fails. Only a newer edit is retried automatically. */
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

  let timer: ReturnType<typeof setTimeout> | null = null;
  let inFlight: Promise<void> | null = null;
  let attemptedDoc = store.getState().document;
  let disposed = false;

  const clearTimer = () => {
    if (timer !== null) clearTimeout(timer);
    timer = null;
  };
  const schedule = () => {
    clearTimer();
    timer = setTimeout(() => {
      timer = null;
      void autosave();
    }, options.autosaveMs);
  };

  // Manual and automatic saves share a single writer. Drain edits made during
  // a PUT before resolving, so an older request can never overwrite a newer one.
  const saveNow = (): Promise<void> => {
    if (disposed) return Promise.resolve();
    if (inFlight) return inFlight;
    clearTimer();
    inFlight = (async () => {
      while (!disposed) {
        const doc = store.getState().document;
        attemptedDoc = doc;
        await api.putAnalysis(analysisId, serializeDocument(doc));
        if (disposed) return;
        if (store.getState().document === doc) {
          store.dispatch({ type: "markSaved" });
          return;
        }
      }
    })().finally(() => {
      inFlight = null;
      clearTimer();
      // A failed PUT may have outlived a newer edit's debounce. Preserve that
      // pending edit, but never repeatedly retry the unchanged failed value.
      if (!disposed && options.autosaveMs !== undefined && store.getState().dirty && store.getState().document !== attemptedDoc)
        schedule();
    });
    return inFlight;
  };

  const autosave = async () => {
    if (inFlight || disposed) return;
    try {
      await saveNow();
    } catch (e) {
      if (!disposed) options.onSaveError?.(e);
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
      clearTimer();
      unsubscribeStore?.();
      unsubscribe();
    },
  };
}
