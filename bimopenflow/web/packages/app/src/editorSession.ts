// Reports the editor's open analysis and selection to the host, so a
// separate process (the stdio MCP server) can read what the studio shows.
// Debounced and deduplicated: report() queues a value and sends it once
// reports have been quiet for the debounce, only when it differs from the
// last one sent; flush() sends the latest value right away.

import type { EditorSession } from "@bimopenflow/contracts";

export interface SessionApi {
  putSession(body: EditorSession): Promise<unknown>;
}

/** Trailing debounce for the selection/analysis report (Design: "Selection is debounced"). */
export const SESSION_DEBOUNCE_MS = 250;

export interface SessionReporter {
  /** Queues the session; sends it once reports have been quiet for the debounce, and only if it differs from the last one sent. */
  report(session: EditorSession): void;
  /** Sends the latest session now, even if unchanged (the tab became visible). */
  flush(): void;
  dispose(): void;
}

const sameSession = (a: EditorSession, b: EditorSession): boolean =>
  a.analysisId === b.analysisId &&
  a.selection.length === b.selection.length &&
  a.selection.every((id, i) => id === b.selection[i]);

export interface SessionReporterOptions {
  debounceMs?: number;
  onError?: (e: unknown) => void;
}

export function createSessionReporter(api: SessionApi, options: SessionReporterOptions = {}): SessionReporter {
  const debounceMs = options.debounceMs ?? SESSION_DEBOUNCE_MS;
  // `latest` is the most recent value passed to report(), kept even after it
  // is sent, so flush() always has something to resend.
  let latest: EditorSession | null = null;
  let lastSent: EditorSession | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let disposed = false;

  const send = (session: EditorSession) => {
    lastSent = session;
    api.putSession(session).catch((e) => {
      // A failed PUT is not remembered as sent, so the next change retries it.
      lastSent = null;
      options.onError?.(e);
    });
  };

  const clearTimer = () => {
    if (timer !== null) clearTimeout(timer);
    timer = null;
  };

  return {
    report(session) {
      if (disposed) return;
      latest = session;
      clearTimer();
      timer = setTimeout(() => {
        timer = null;
        if (latest === null) return;
        const next = latest;
        if (lastSent !== null && sameSession(lastSent, next)) return;
        send(next);
      }, debounceMs);
    },
    flush() {
      if (disposed || latest === null) return;
      clearTimer();
      send(latest);
    },
    dispose() {
      disposed = true;
      clearTimer();
    },
  };
}
