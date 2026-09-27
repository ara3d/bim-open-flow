// The client half of POST /api/ask's protocol (see AskEndpoint.cs and
// AskHandler.cs): the event shape, the SSE reader, and the transcript-line
// renderer. Shared by the DuckDB demo page's Ask box (duckdbDemo.ts) and the
// main editor's Ask panel (askPanel.ts, TKT-84) so the two never drift.

/** One line of the server-sent event stream POST /api/ask answers with. */
export interface AskEvent {
  type: 'start' | 'tool' | 'text' | 'check' | 'done' | 'error';
  verified?: boolean;
  problem?: string | null;
  analysisId?: string;
  model?: string;
  effort?: string | null;
  name?: string;
  args?: Record<string, unknown> | null;
  ok?: boolean | null;
  summary?: string | null;
  text?: string | null;
  message?: string;
  built?: boolean;
  turns?: number;
  inputTokens?: number;
  outputTokens?: number;
}

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

/** Reads a `text/event-stream` body of `data: <json>\n\n` frames, calling
 *  `onEvent` with each parsed payload in order. */
export async function readEvents(response: Response, onEvent: (event: AskEvent) => Promise<void> | void): Promise<void> {
  const reader = response.body!.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let end: number;
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const chunk = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      const data = chunk.split('\n').filter(l => l.startsWith('data: ')).map(l => l.slice(6)).join('\n');
      if (data) await onEvent(JSON.parse(data) as AskEvent);
    }
  }
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

/** POSTs a request to /api/ask and streams its events to `onEvent`; throws
 *  when the response is not OK or carries no body. */
export async function postAsk(
  fetchFn: typeof fetch,
  body: { request: string; analysisId?: string },
  onEvent: (event: AskEvent) => Promise<void> | void,
): Promise<void> {
  const response = await fetchFn('/api/ask', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok || !response.body) throw new Error(`${response.status} ${response.statusText}`);
  await readEvents(response, onEvent);
}
