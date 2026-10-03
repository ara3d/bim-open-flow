// The page side of the Ask protocol (AskEndpoint.cs, AskHandler.cs): the
// model probe and the transcript-line renderer. The event shape, the reader,
// and the transport are in @bimopenflow/client/host (askEvents.ts). Shared by the
// DuckDB demo page's Ask box (duckdbDemo.ts) and the main editor's Ask panel
// (askPanel.ts, TKT-84) so the two never drift.

/** GET /api/ask/model's payload (AskEndpoint.ModelInfoPayload). */
export interface AskModelInfo {
  model: string;
  provider?: string;
  effort?: string | null;
  executable?: string | null;
  configured: boolean;
  problem: string | null;
}

/** Appends one transcript line to `log` and scrolls it into view; returns the
 *  element. `prefix` names the CSS classes (`${prefix}-line ${prefix}-${kind}`),
 *  so each page can style its own transcript without sharing a stylesheet. */
export function appendAskLine(log: HTMLElement, kind: string, text: string, prefix = 'duck-ask'): HTMLElement {
  const item = document.createElement('div');
  item.className = `${prefix}-line ${prefix}-${kind}`;
  item.textContent = text;
  log.append(item);
  log.scrollTop = log.scrollHeight;
  return item;
}

/** A tool call's arguments, `id` dropped (it is always the open analysis) and
 *  truncated so one line never dominates the transcript. */
export function shortArgs(args: Record<string, unknown> | null | undefined): string {
  if (!args) return '';
  const shown = Object.entries(args).filter(([key]) => key !== 'id')
    .map(([key, value]) => `${key}=${typeof value === 'string' ? JSON.stringify(value) : String(value)}`).join(', ');
  return shown.length > 140 ? shown.slice(0, 140) + '…' : shown;
}

/** GET /api/ask/model; null when the host has no /api/ask at all (a 404, or a
 *  network failure), which is how callers feature-detect the endpoint without
 *  throwing on a plain host that does not offer it. */
export async function probeAsk(fetchFn: typeof fetch): Promise<AskModelInfo | null> {
  try {
    const response = await fetchFn('/api/ask/model');
    if (!response.ok) return null;
    return await response.json() as AskModelInfo;
  } catch {
    return null;
  }
}
