import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createSessionReporter, SESSION_DEBOUNCE_MS } from "../src/editorSession.js";

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("createSessionReporter", () => {
  it("collapses quick reports into one PUT with the last value", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const reporter = createSessionReporter({ putSession });

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
    const reporter = createSessionReporter({ putSession });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);
  });

  it("flush resends the latest value immediately, even unchanged", async () => {
    const putSession = vi.fn().mockResolvedValue({});
    const reporter = createSessionReporter({ putSession });

    reporter.report({ analysisId: "a", selection: ["x"] });
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).toHaveBeenCalledTimes(1);

    reporter.flush();
    await vi.advanceTimersByTimeAsync(0);
    expect(putSession).toHaveBeenCalledTimes(2);
    expect(putSession).toHaveBeenLastCalledWith({ analysisId: "a", selection: ["x"] });
  });

  it("reports a failed PUT to onError, and retries at the next change", async () => {
    const onError = vi.fn();
    const putSession = vi.fn()
      .mockRejectedValueOnce(new Error("404"))
      .mockResolvedValueOnce({});
    const reporter = createSessionReporter({ putSession }, { onError });

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
    const reporter = createSessionReporter({ putSession });
    reporter.report({ analysisId: "a", selection: [] });
    reporter.dispose();
    await vi.advanceTimersByTimeAsync(SESSION_DEBOUNCE_MS);
    expect(putSession).not.toHaveBeenCalled();
  });
});
