// askClient.ts is the client half of POST /api/ask's protocol (AskEndpoint.cs,
// AskHandler.cs), shared by duckdbDemo.ts and askPanel.ts (TKT-84). These
// tests exercise it directly, independent of either page.
import { describe, expect, it } from "vitest";
import { appendAskLine, probeAsk, shortArgs } from "../src/askClient.js";

describe("appendAskLine", () => {
  it("uses the given prefix for both the line and kind classes", () => {
    const log = document.createElement("div");
    const line = appendAskLine(log, "you", "You: hello", "bof-ask");
    expect(line.className).toBe("bof-ask-line bof-ask-you");
    expect(line.textContent).toBe("You: hello");
    expect(log.contains(line)).toBe(true);
  });

  it("defaults to the duck-ask prefix", () => {
    const log = document.createElement("div");
    const line = appendAskLine(log, "bad", "Error: boom");
    expect(line.className).toBe("duck-ask-line duck-ask-bad");
  });

  it("scrolls the log to the newest line", () => {
    const log = document.createElement("div");
    Object.defineProperty(log, "scrollHeight", { configurable: true, get: () => 42 });
    appendAskLine(log, "note", "hi");
    expect(log.scrollTop).toBe(42);
  });
});

describe("shortArgs", () => {
  it("drops the id and formats the rest", () => {
    expect(shortArgs({ id: "ask-1", nodeId: "n1", kind: "duck.source" })).toBe('nodeId="n1", kind="duck.source"');
  });

  it("returns an empty string for no args", () => {
    expect(shortArgs(undefined)).toBe("");
    expect(shortArgs(null)).toBe("");
  });

  it("truncates a long rendering", () => {
    const long = shortArgs({ query: "x".repeat(200) });
    expect(long.length).toBe(141);
    expect(long.endsWith("…")).toBe(true);
  });
});

describe("probeAsk", () => {
  it("returns the model info when the host answers", async () => {
    const info = { model: "m", configured: true, problem: null };
    const fetchFn = (async () => new Response(JSON.stringify(info), { status: 200 })) as typeof fetch;
    await expect(probeAsk(fetchFn)).resolves.toEqual(info);
  });

  it("returns null on a 404 (no /api/ask on this host)", async () => {
    const fetchFn = (async () => new Response("not found", { status: 404 })) as typeof fetch;
    await expect(probeAsk(fetchFn)).resolves.toBeNull();
  });

  it("returns null on a network failure", async () => {
    const fetchFn = (async () => { throw new Error("offline"); }) as typeof fetch;
    await expect(probeAsk(fetchFn)).resolves.toBeNull();
  });
});
