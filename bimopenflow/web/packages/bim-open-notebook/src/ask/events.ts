// The /api/ask event stream: its event shape and a reader for it.
// Duplicates the page-local reader in packages/app/src/duckdbDemo.ts; the
// plan (docs/plans/notebook.md, "Debt") says how the two become one.

/** One server-sent event from POST /api/ask (src/studio/BimOpenFlow.Studio/AskHandler.cs). */
export interface AskEvent {
  readonly type: "start" | "tool" | "text" | "check" | "done" | "error";
  readonly analysisId?: string;
  readonly model?: string;
  readonly effort?: string;
  readonly continuing?: boolean;
  readonly name?: string;
  readonly args?: Record<string, unknown> | null;
  readonly ok?: boolean | null;
  readonly summary?: string | null;
  readonly text?: string | null;
  readonly message?: string;
  readonly built?: boolean;
  readonly verified?: boolean;
  readonly problem?: string | null;
  readonly turns?: number;
  readonly inputTokens?: number;
  readonly outputTokens?: number;
}

/** Sends one request to the agent and streams its events. */
export interface AskTransport {
  /** Resolves after the last event; rejects when the host refuses the request. */
  ask(
    request: string,
    analysisId: string | undefined,
    onEvent: (event: AskEvent) => void,
    signal?: AbortSignal,
  ): Promise<void>;
}

export const ASK_ROUTE = "/api/ask";

/**
 * Reads `data: <json>` blocks separated by blank lines until the body ends.
 * An event that is not a JSON object with a `type` arrives as an error event
 * and the stream goes on. An abort of `signal` cancels the body and rejects
 * with the signal's reason.
 */
export async function readAskEvents(
  response: Response,
  onEvent: (event: AskEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  if (!response.body) throw new Error(`${ASK_ROUTE} answered ${response.status} with no body`);
  const reader = response.body.getReader();
  const cancel = () => void reader.cancel(signal?.reason).catch(() => undefined);
  signal?.addEventListener("abort", cancel, { once: true });
  const decoder = new TextDecoder();
  let buffer = "";
  try {
    for (;;) {
      signal?.throwIfAborted();
      const { value, done } = await reader.read();
      signal?.throwIfAborted();
      // A "\r" at the end of one chunk pairs with a "\n" at the start of the next.
      buffer = (buffer + (done ? decoder.decode() : decoder.decode(value, { stream: true }))).replace(/\r\n/g, "\n");
      let end: number;
      while ((end = buffer.indexOf("\n\n")) >= 0) {
        deliver(buffer.slice(0, end), onEvent);
        buffer = buffer.slice(end + 2);
      }
      if (done) break;
    }
    // A last event the host did not close with a blank line.
    deliver(buffer, onEvent);
  } finally {
    signal?.removeEventListener("abort", cancel);
  }
}

/** One block: its `data:` lines joined by newlines, parsed. Other SSE fields are ignored. */
function deliver(block: string, onEvent: (event: AskEvent) => void): void {
  const data = block
    .split("\n")
    .filter((line) => line.startsWith("data:"))
    .map((line) => line.slice(line.startsWith("data: ") ? 6 : 5))
    .join("\n");
  if (data.trim() === "") return;
  onEvent(parseEvent(data));
}

function parseEvent(data: string): AskEvent {
  try {
    const parsed: unknown = JSON.parse(data);
    if (parsed !== null && typeof parsed === "object" && typeof (parsed as { type?: unknown }).type === "string")
      return parsed as AskEvent;
    return { type: "error", message: `Unreadable event from ${ASK_ROUTE} (no type): ${data}` };
  } catch (e) {
    return { type: "error", message: `Unreadable event from ${ASK_ROUTE}: ${e instanceof Error ? e.message : String(e)}` };
  }
}

/** POST /api/ask through `fetchFn` (same origin), body { request, analysisId? }. */
export function createAskTransport(fetchFn: typeof fetch): AskTransport {
  return {
    async ask(request, analysisId, onEvent, signal) {
      const response = await fetchFn(ASK_ROUTE, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(analysisId ? { request, analysisId } : { request }),
        signal,
      });
      if (!response.ok) {
        const message = (await response.text().catch(() => "")).trim() || response.statusText;
        throw new Error(`POST ${ASK_ROUTE} -> ${response.status}: ${message}`);
      }
      await readAskEvents(response, onEvent, signal);
    },
  };
}
