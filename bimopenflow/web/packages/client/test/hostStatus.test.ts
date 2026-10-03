import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createHostStatus,
  hostStatusMessage,
  initialHostStatus,
  probeIntervalMs,
  reduceHostStatus,
  watchHost,
  type HostStatusState,
} from "../src/hostStatus.js";

const fail = (state: HostStatusState) => reduceHostStatus(state, { type: "failure", reason: "boom" });
const ok = (state: HostStatusState) => reduceHostStatus(state, { type: "success" });

describe("reduceHostStatus", () => {
  it("starts as reconnecting and connects on the first success", () => {
    expect(initialHostStatus.status).toBe("reconnecting");
    expect(ok(initialHostStatus)).toEqual({ status: "connected", failures: 0 });
  });

  it("one failure means reconnecting, a second means offline", () => {
    const once = fail(ok(initialHostStatus));
    expect(once).toEqual({ status: "reconnecting", failures: 1, reason: "boom" });
    expect(fail(once).status).toBe("offline");
    expect(fail(fail(once)).failures).toBe(3);
  });

  it("a success after failures reconnects and resets the count", () => {
    expect(ok(fail(fail(initialHostStatus)))).toEqual({ status: "connected", failures: 0 });
  });

  it("returns the same state object for a success while connected", () => {
    const connected = ok(initialHostStatus);
    expect(ok(connected)).toBe(connected);
  });
});

describe("hostStatusMessage and probe cadence", () => {
  it("names the API url when offline and says nothing when connected", () => {
    expect(hostStatusMessage("connected", "/api")).toBe("");
    expect(hostStatusMessage("offline", "http://x/api")).toContain("http://x/api");
    expect(hostStatusMessage("reconnecting", "/api")).toMatch(/Reconnecting/);
  });

  it("probes faster while not connected", () => {
    expect(probeIntervalMs("connected")).toBe(10_000);
    expect(probeIntervalMs("offline")).toBe(5_000);
    expect(probeIntervalMs("connected")).toBeGreaterThan(probeIntervalMs("offline"));
    expect(probeIntervalMs("reconnecting")).toBe(probeIntervalMs("offline"));
  });
});

const response = (status: number) => new Response(null, { status });

describe("createHostStatus", () => {
  afterEach(() => vi.useRealTimers());

  it("reports a successful fetch and a 5xx through the wrapped fetch", async () => {
    const inner = vi.fn<typeof fetch>().mockResolvedValueOnce(response(200)).mockResolvedValueOnce(response(503));
    const host = createHostStatus({ fetch: inner });
    await host.fetch("/api/models");
    expect(host.get().status).toBe("connected");
    await host.fetch("/api/models");
    expect(host.get()).toMatchObject({ status: "reconnecting", reason: "HTTP 503" });
  });

  it("treats a 4xx as reachable", async () => {
    const host = createHostStatus({ fetch: vi.fn<typeof fetch>().mockResolvedValue(response(404)) });
    await host.fetch("/api/analyses/x");
    expect(host.get().status).toBe("connected");
  });

  it("reports a network error and rethrows it", async () => {
    const host = createHostStatus({ fetch: vi.fn<typeof fetch>().mockRejectedValue(new TypeError("Failed to fetch")) });
    await expect(host.fetch("/api/models")).rejects.toThrow("Failed to fetch");
    expect(host.get()).toMatchObject({ status: "reconnecting", reason: "Failed to fetch" });
  });

  it("notifies subscribers only on change and stops after unsubscribe", async () => {
    const host = createHostStatus({ fetch: vi.fn<typeof fetch>().mockResolvedValue(response(200)) });
    const seen: string[] = [];
    const unsubscribe = host.subscribe((s) => seen.push(s.status));
    await host.fetch("/a");
    await host.fetch("/b");
    host.reportFailure("stream closed");
    unsubscribe();
    host.reportFailure("again");
    expect(seen).toEqual(["connected", "reconnecting"]);
  });

  it("probes at once, then on the disconnected cadence until connected, then on the slow one", async () => {
    vi.useFakeTimers();
    const inner = vi.fn<typeof fetch>().mockRejectedValue(new TypeError("refused"));
    const host = createHostStatus({ fetch: inner, probeInterval: (s) => (s === "connected" ? 150 : 50) });
    host.start(() => host.fetch("/api/models").catch(() => undefined));
    await vi.advanceTimersByTimeAsync(0);
    expect(inner).toHaveBeenCalledTimes(1);
    expect(host.get().status).toBe("reconnecting");
    await vi.advanceTimersByTimeAsync(50);
    expect(inner).toHaveBeenCalledTimes(2);
    expect(host.get().status).toBe("offline");
    inner.mockResolvedValue(response(200));
    await vi.advanceTimersByTimeAsync(50);
    expect(host.get().status).toBe("connected");
    await vi.advanceTimersByTimeAsync(100);
    expect(inner).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(50);
    expect(inner).toHaveBeenCalledTimes(4);
    host.dispose();
    await vi.advanceTimersByTimeAsync(1000);
    expect(inner).toHaveBeenCalledTimes(4);
  });

  it("watchHost builds the client on the reporting fetch and probes with listModels", async () => {
    vi.useFakeTimers();
    const inner = vi.fn<typeof fetch>().mockResolvedValue(response(200));
    const { api, host } = watchHost((f) => ({ listModels: () => f("/api/models") }), { fetch: inner, probeInterval: () => 10 });
    expect(api).toBeDefined();
    await vi.advanceTimersByTimeAsync(0);
    expect(inner).toHaveBeenCalledWith("/api/models", undefined);
    expect(host.get().status).toBe("connected");
    host.dispose();
  });
});
