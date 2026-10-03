// One source of truth for "is the host API reachable". Fed by every API call
// (a fetch wrapper), the evaluation event stream, and a periodic probe. The
// reducer is pure; createHostStatus is the thin timer runtime around it. No DOM.

export type HostStatus = "connected" | "reconnecting" | "offline";

export interface HostStatusState {
  status: HostStatus;
  /** Consecutive failures since the last success. */
  failures: number;
  /** The most recent failure's description, kept for the banner. */
  reason?: string;
}

export type HostEvent =
  | { type: "success" }
  | { type: "failure"; reason: string };

/** Failures before "reconnecting" becomes "offline". */
export const OFFLINE_AFTER = 2;

/** Nothing has answered yet: treat as reconnecting until the first result. */
export const initialHostStatus: HostStatusState = { status: "reconnecting", failures: 0 };

export function reduceHostStatus(state: HostStatusState, event: HostEvent): HostStatusState {
  if (event.type === "success")
    return state.status === "connected" ? state : { status: "connected", failures: 0 };
  const failures = state.failures + 1;
  return { status: failures >= OFFLINE_AFTER ? "offline" : "reconnecting", failures, reason: event.reason };
}

/**
 * Probe cadence: cheap while connected, eager while not. An idle page learns
 * of a host death only from this probe (the event stream's onerror is slow),
 * so the connected cadence bounds the time to the offline banner: about the
 * interval plus the second failure needed for "offline".
 */
export const probeIntervalMs = (status: HostStatus): number => (status === "connected" ? 10_000 : 5_000);

/** What the banner says; empty while connected. */
export function hostStatusMessage(status: HostStatus, apiUrl: string): string {
  switch (status) {
    case "connected": return "";
    case "reconnecting": return `Reconnecting to the host at ${apiUrl}…`;
    case "offline": return `Host not reachable at ${apiUrl}. Start it and it will reconnect.`;
  }
}

/** Response statuses that mean the host (or the proxy in front of it) is down. */
export const isHostFailure = (status: number): boolean => status >= 500;

export interface HostStatusSource {
  /** Pass to `new ApiClient({ fetch })`: every call reports its outcome. */
  fetch: typeof fetch;
  get(): HostStatusState;
  subscribe(listener: (state: HostStatusState) => void): () => void;
  reportFailure(reason: string): void;
  reportSuccess(): void;
  /** Probes now and then periodically with `probe` (its outcome is reported through `fetch`). */
  start(probe: () => Promise<unknown>): void;
  dispose(): void;
}

export interface HostStatusOptions {
  fetch?: typeof fetch;
  /** Overrides probeIntervalMs (tests). */
  probeInterval?: (status: HostStatus) => number;
}

export function createHostStatus(options: HostStatusOptions = {}): HostStatusSource {
  const inner = options.fetch ?? fetch.bind(globalThis);
  const interval = options.probeInterval ?? probeIntervalMs;
  const listeners = new Set<(state: HostStatusState) => void>();
  let state = initialHostStatus;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let probe: (() => Promise<unknown>) | null = null;
  let disposed = false;

  const dispatch = (event: HostEvent) => {
    const next = reduceHostStatus(state, event);
    if (next === state) return;
    const changed = next.status !== state.status;
    state = next;
    for (const listener of listeners) listener(state);
    // A status change moves the probe onto the new cadence.
    if (changed) schedule();
  };

  const schedule = (delay = interval(state.status)) => {
    if (timer !== null) clearTimeout(timer);
    timer = null;
    if (disposed || !probe) return;
    timer = setTimeout(() => {
      timer = null;
      // Failures are reported by the wrapped fetch; a non-network error is not a host failure.
      probe!().then(() => schedule(), () => schedule());
    }, delay);
  };

  const wrapped: typeof fetch = async (input, init) => {
    let response: Response;
    try {
      response = await inner(input, init);
    } catch (error) {
      dispatch({ type: "failure", reason: error instanceof Error ? error.message : String(error) });
      throw error;
    }
    dispatch(isHostFailure(response.status)
      ? { type: "failure", reason: `HTTP ${response.status}` }
      : { type: "success" });
    return response;
  };

  return {
    fetch: wrapped,
    get: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => { listeners.delete(listener); };
    },
    reportFailure: (reason) => dispatch({ type: "failure", reason }),
    reportSuccess: () => dispatch({ type: "success" }),
    start(fn) {
      probe = fn;
      schedule(0);
    },
    dispose() {
      disposed = true;
      if (timer !== null) clearTimeout(timer);
      timer = null;
      listeners.clear();
    },
  };
}

/**
 * Builds an API client whose every call feeds a host status, and probes with
 * the client's cheapest call. `make` receives the reporting fetch.
 */
export function watchHost<A extends { listModels(): Promise<unknown> }>(
  make: (fetchFn: typeof fetch) => A,
  options: HostStatusOptions = {},
): { api: A; host: HostStatusSource } {
  const host = createHostStatus(options);
  const api = make(host.fetch);
  host.start(() => api.listModels());
  return { api, host };
}
