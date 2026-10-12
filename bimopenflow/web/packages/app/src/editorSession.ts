// Reports the editor's open analysis and selection to the host, so a
// separate process (the stdio MCP server) can read what the studio shows.
// Debounced and deduplicated: report() queues a value and sends it once
// reports have been quiet for the debounce, only when it differs from the
// last one sent. reclaim() (the tab became visible) reads the host's session
// after the same debounce and resends this tab's only when another tab has
// replaced it, so a pane that toggles visibility many times a second costs
// one read and no write (TKT-255).

import type { EditorSession } from "@bimopenflow/contracts";

export interface SessionApi {
  putSession(body: EditorSession): Promise<unknown>;
  getSession(): Promise<EditorSession>;
}

/** Trailing debounce for the selection/analysis report (Design: "Selection is debounced"). */
export const SESSION_DEBOUNCE_MS = 250;

export interface SessionReporter {
  /** Queues the session; sends it once reports have been quiet for the debounce, and only if it differs from the last one sent. */
  report(session: EditorSession): void;
  /** The tab became visible: after the debounce, reads the host's session and
   *  resends the latest one only when the host holds a different one. Calls
   *  during the debounce or the read join it. */
  reclaim(): void;
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
  // is sent, so reclaim() always has something to resend.
  let latest: EditorSession | null = null;
  let lastSent: EditorSession | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;
  /** True from the first reclaim() of a burst until its read has answered. */
  let reclaiming = false;
  let reclaimTimer: ReturnType<typeof setTimeout> | null = null;
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

  const resendIfReplaced = async () => {
    reclaimTimer = null;
    // A pending report sends the latest value itself.
    if (disposed || latest === null || timer !== null) {
      reclaiming = false;
      return;
    }
    try {
      const held = await api.getSession();
      if (!disposed && latest !== null && timer === null && !sameSession(held, latest)) send(latest);
    } catch (e) {
      if (!disposed) options.onError?.(e);
    } finally {
      reclaiming = false;
    }
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
    reclaim() {
      if (disposed || latest === null || reclaiming) return;
      reclaiming = true;
      reclaimTimer = setTimeout(() => void resendIfReplaced(), debounceMs);
    },
    dispose() {
      disposed = true;
      clearTimer();
      if (reclaimTimer !== null) clearTimeout(reclaimTimer);
      reclaimTimer = null;
    },
  };
}
