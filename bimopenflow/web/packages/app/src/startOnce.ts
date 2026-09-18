// A start routine that several triggers may fire (page load, host reconnect)
// but that must run once at a time and never again after it has succeeded.
// While one attempt is in flight, further triggers join it; a failed attempt
// lets the next trigger try again.

export type StartOnce = () => Promise<void>;

/** Wraps `start` so overlapping calls share one attempt and success is permanent. */
export const startOnce = (start: () => Promise<void>, succeeded: () => boolean): StartOnce => {
  let inFlight: Promise<void> | undefined;
  return () => {
    if (succeeded()) return Promise.resolve();
    if (!inFlight)
      inFlight = start().finally(() => { if (!succeeded()) inFlight = undefined; });
    return inFlight;
  };
};
