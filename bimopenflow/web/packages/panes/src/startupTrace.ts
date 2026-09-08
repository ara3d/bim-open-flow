/** Opt-in User Timing spans, visible in DevTools and the startup benchmark. */
export function startupTrace() {
  const enabled = typeof location !== "undefined" && new URLSearchParams(location.search).has("profileStartup");
  const record = (name: string, start: number) => {
    if (enabled) performance.measure(`bimflow:${name}`, { start, end: performance.now() });
  };
  return {
    enabled,
    span<T>(name: string, action: () => T): T {
      if (!enabled) return action();
      const start = performance.now();
      try { return action(); } finally { record(name, start); }
    },
    record,
  };
}
