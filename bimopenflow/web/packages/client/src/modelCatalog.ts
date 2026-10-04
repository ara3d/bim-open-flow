import type { ModelSummary } from "@bimopenflow/contracts";
import { matchModelId } from "./modelRef";

/** Shares catalog requests across simultaneous pane updates; failed fetches can retry. */
export function modelCatalog(list: () => Promise<ModelSummary[]>) {
  let models: ModelSummary[] | undefined;
  let pending: Promise<ModelSummary[]> | undefined;
  const refresh = () => pending ??= list().then(value => models = value).finally(() => { pending = undefined; });
  return async (path: string): Promise<string | null> => {
    const current = models ?? await refresh();
    const id = matchModelId(current, path);
    if (id) return id;
    // A model may have appeared since the cached catalog was fetched.
    return matchModelId(await refresh(), path) ?? null;
  };
}
