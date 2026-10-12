import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createSessionReporter, SESSION_DEBOUNCE_MS } from "../src/editorSession.js";

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("createSessionReporter", () => {
  it("collapses quick reports into one PUT with the last value", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const reporter = createSessionReporter({ putSession, getSession: vi.fn() });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS - 1);
    reporter.report({ analysisId: "a", selection: ["x", "y"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS - 1);
    reporter.report({ analysisId: "a", selection: ["x", "y", "z"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);

    expect(putSession).toHaveBeenCalledTimes(1);
    expect(putSession).toHaveBeenCalledWith({ analysisId: "a", selection: ["x", "y", "z"] });
  });

  it("sends nothing when the reported session is unchanged from the last one sent", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const reporter = createSessionReporter({ putSession, getSession: vi.fn() });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);
  });

  it("reclaim sends nothing while the host holds this tab's session, however often it is called (TKT-255)", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const getSession = vi.fn().mockResolvedValue({ analysisId: "a", selection: ["x"] });
    const reporter = createSessionReporter({ putSession, getSession });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);

    for (let i = 0; i < 150; i++) reporter.reclaim();
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(getSession).toHaveBeenCalledTimes(1);
    expect(putSession).toHaveBeenCalledTimes(1);
  });

  it("reclaim resends the latest value once when another tab replaced it", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const getSession = vi.fn().mockResolvedValue({ analysisId: "other", selection: [] });
    const reporter = createSessionReporter({ putSession, getSession });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    for (let i = 0; i < 150; i++) reporter.reclaim();
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(2);
    expect(putSession).toHaveBeenLastCalledWith({ analysisId: "a", selection: ["x"] });
  });

  it("reclaim leaves a pending report to send the latest value", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const getSession = vi.fn().mockResolvedValue({ selection: [] });
    const reporter = createSessionReporter({ putSession, getSession }, { debounceMs: 100 });
    reporter.report({ analysisId: "a", selection: [] });
    reporter.reclaim();
    await vi.advanceTimersByTimeAsync(50);
    reporter.report({ analysisId: "a", selection: ["y"] });
    await vi.advanceTimersByTimeAsync(200);
    expect(getSession).not.toHaveBeenCalled();
    expect(putSession).toHaveBeenCalledTimes(1);
  });

  it("reports a failed PUT to onError, and retries at the next change", async () => {
    const onError = vi.fn();
    const putSession = vi.fn()
      .mockRejectedValueOnce(new Error("404"))
      .mockResolvedValueOnce({});
    const reporter = createSessionReporter({ putSession, getSession: vi.fn() }, { onError });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(onError).toHaveBeenCalledTimes(1);

    reporter.report({ analysisId: "a", selection: ["x", "y"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);

    expect(putSession).toHaveBeenCalledTimes(2);
    expect(onError).toHaveBeenCalledTimes(1);
  });

  it("dispose stops further sends", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const reporter = createSessionReporter({ putSession, getSession: vi.fn() });
    reporter.report({ analysisId: "a", selection: [] });
    reporter.dispose();
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).not.toHaveBeenCalled();
  });
});
