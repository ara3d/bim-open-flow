// askClient.ts is the client half of POST /api/ask's protocol (AskEndpoint.cs,
// AskHandler.cs), shared by duckdbDemo.ts and askPanel.ts (TKT-84). These
// tests exercise it directly, independent of either page.
import { describe, expect, it } from "vitest";
import { appendAskLine, postAsk, probeAsk, readEvents, shortArgs, type AskEvent } from "../src/askClient.js";

/** A `Response` whose body streams `frames` (already-formatted SSE text) one
 *  chunk at a time, the way a real fetch would deliver them piecemeal. */
function sseResponse(frames: string[], init: { ok?: boolean; status?: number } = {}): Response {
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const frame of frames) controller.enqueue(encoder.encode(frame));
      controller.close();
    },
  });
  return new Response(body, { status: init.status ?? 200 });
}

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

describe("readEvents", () => {
  it("parses one JSON object per data: frame, in order", async () => {
    const response = sseResponse([
      'data: {"type":"start","analysisId":"a1"}\n\n',
      'data: {"type":"done","built":true}\n\n',
    ]);
    const seen: AskEvent[] = [];
    await readEvents(response, (event) => { seen.push(event); });
    expect(seen).toEqual([{ type: "start", analysisId: "a1" }, { type: "done", built: true }]);
  });

  it("reassembles a frame split across two chunks", async () => {
    const response = sseResponse(['data: {"typ', 'e":"text","text":"hi"}\n\n']);
    const seen: AskEvent[] = [];
    await readEvents(response, (event) => { seen.push(event); });
    expect(seen).toEqual([{ type: "text", text: "hi" }]);
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

describe("postAsk", () => {
  it("posts the request and analysis id, and streams the events", async () => {
    let sentBody: unknown;
    const fetchFn = (async (_url, init) => {
      sentBody = JSON.parse(String(init!.body));
      return sseResponse(['data: {"type":"start","analysisId":"a1"}\n\n']);
    }) as typeof fetch;
    const seen: AskEvent[] = [];
    await postAsk(fetchFn, { request: "add a filter", analysisId: "a1" }, (e) => { seen.push(e); });
    expect(sentBody).toEqual({ request: "add a filter", analysisId: "a1" });
    expect(seen).toEqual([{ type: "start", analysisId: "a1" }]);
  });

  it("throws when the response is not OK", async () => {
    const fetchFn = (async () => new Response(null, { status: 500, statusText: "Internal Server Error" })) as typeof fetch;
    await expect(postAsk(fetchFn, { request: "x" }, () => {})).rejects.toThrow("500");
  });
});
