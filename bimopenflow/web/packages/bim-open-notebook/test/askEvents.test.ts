import { describe, expect, it } from "vitest";
import { createAskTransport, readAskEvents, type AskEvent } from "../src/ask/events";

const encoder = new TextEncoder();

/** A streamed response whose body arrives as these chunks (strings or raw bytes). */
function streamOf(chunks: readonly (string | Uint8Array)[], init?: ResponseInit): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(typeof chunk === "string" ? encoder.encode(chunk) : chunk);
      controller.close();
    },
  });
  return new Response(body, init);
}

/** Cuts `text` at the given offsets. */
function cut(text: string, ...at: number[]): string[] {
  const edges = [0, ...at, text.length];
  return edges.slice(1).map((end, i) => text.slice(edges[i], end));
}

async function read(response: Response): Promise<AskEvent[]> {
  const events: AskEvent[] = [];
  await readAskEvents(response, (e) => events.push(e));
  return events;
}

// Recorded from the studio for "total operational carbon of the building" (tokens shortened).
const recorded =
  'data: {"type":"start","analysisId":"ask-total-operational-carbon","model":"claude-haiku-4-5","effort":"medium","continuing":false}\n\n' +
  'data: {"type":"tool","name":"editGraph","args":{"id":"ask-total-operational-carbon"},"ok":true,"summary":"2 nodes, 1 edge","text":null}\n\n' +
  'data: {"type":"text","name":null,"args":null,"ok":null,"summary":null,"text":"The total is 37,196.2 kgCO2e/yr."}\n\n' +
  'data: {"type":"done","analysisId":"ask-total-operational-carbon","built":true,"verified":true,"problem":null,"text":"The total is 37,196.2 kgCO2e/yr.","turns":4,"inputTokens":9120,"outputTokens":310,"model":"claude-haiku-4-5","effort":"medium"}\n\n';

describe("readAskEvents", () => {
  it("reads several events from one chunk", async () => {
    const events = await read(streamOf([recorded]));
    expect(events.map((e) => e.type)).toEqual(["start", "tool", "text", "done"]);
    expect(events[3].turns).toBe(4);
  });

  it("joins events split across chunks at awkward places", async () => {
    // Inside "data:", inside the JSON, and between the two newlines that end an event.
    const first = recorded.indexOf("\n\n");
    const chunks = cut(recorded, 3, 40, first + 1, first + 2, recorded.length - 1);
    expect(await read(streamOf(chunks))).toEqual(await read(streamOf([recorded])));
  });

  it("accepts \r\n line endings, even with \r and \n in different chunks", async () => {
    const crlf = recorded.replace(/\n/g, "\r\n");
    const at = crlf.indexOf("\r\n\r\n") + 1;
    const events = await read(streamOf(cut(crlf, at, at + 2)));
    expect(events.map((e) => e.type)).toEqual(["start", "tool", "text", "done"]);
  });

  it("joins multi-line data with newlines", async () => {
    const events = await read(streamOf(['data: {"type":"text",\ndata: "text":"two"}\n\n']));
    expect(events).toEqual([{ type: "text", text: "two" }]);
  });

  it("decodes a character whose bytes span two chunks", async () => {
    const bytes = encoder.encode('data: {"type":"text","text":"37 196,2 kgCO₂e"}\n\n');
    const at = bytes.indexOf(0xe2) + 1; // inside the three bytes of the subscript two
    const events = await read(streamOf([bytes.slice(0, at), bytes.slice(at)]));
    expect(events[0].text).toBe("37 196,2 kgCO₂e");
  });

  it("reports a malformed event as an error event and reads on", async () => {
    const events = await read(streamOf(['data: {"type":"text",\n\n', 'data: [1]\n\n', recorded]));
    expect(events.slice(0, 2).map((e) => e.type)).toEqual(["error", "error"]);
    expect(events[0].message).toMatch(/Unreadable event/);
    expect(events.slice(2).map((e) => e.type)).toEqual(["start", "tool", "text", "done"]);
  });

  it("delivers a last event that has no closing blank line, and ignores other fields", async () => {
    const events = await read(streamOf([': keep-alive\n\nevent: x\ndata: {"type":"error","message":"stop"}']));
    expect(events).toEqual([{ type: "error", message: "stop" }]);
  });
});

/** A fetch that records its call and answers with `respond`. */
function fakeFetch(respond: (init: RequestInit) => Response) {
  const calls: { url: string; init: RequestInit }[] = [];
  const fetchFn = (async (url: string, init: RequestInit) => {
    calls.push({ url, init });
    return respond(init);
  }) as unknown as typeof fetch;
  return { fetchFn, calls };
}

describe("createAskTransport", () => {
  it("posts the request as JSON and streams the events", async () => {
    const { fetchFn, calls } = fakeFetch(() => streamOf(cut(recorded, 17, 200)));
    const events: AskEvent[] = [];
    await createAskTransport(fetchFn).ask("total operational carbon", undefined, (e) => events.push(e));
    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe("/api/ask");
    expect(calls[0].init.method).toBe("POST");
    expect(new Headers(calls[0].init.headers).get("content-type")).toBe("application/json");
    expect(JSON.parse(calls[0].init.body as string)).toEqual({ request: "total operational carbon" });
    expect(events.map((e) => e.type)).toEqual(["start", "tool", "text", "done"]);
  });

  it("sends the analysis id of a follow-up", async () => {
    const { fetchFn, calls } = fakeFetch(() => streamOf([]));
    await createAskTransport(fetchFn).ask("now per storey", "ask-total-operational-carbon", () => undefined);
    expect(JSON.parse(calls[0].init.body as string)).toEqual({
      request: "now per storey",
      analysisId: "ask-total-operational-carbon",
    });
  });

  it("rejects with the host's message on a non-2xx status", async () => {
    const { fetchFn } = fakeFetch(() => new Response("request body is not valid JSON", { status: 400 }));
    await expect(createAskTransport(fetchFn).ask("x", undefined, () => undefined)).rejects.toThrow(
      "POST /api/ask -> 400: request body is not valid JSON",
    );
  });

  it("stops reading and rejects when the signal aborts", async () => {
    // Sends the start event, then holds the stream open like an agent at work.
    const { fetchFn, calls } = fakeFetch(
      () =>
        new Response(
          new ReadableStream<Uint8Array>({
            start(controller) {
              controller.enqueue(encoder.encode(recorded.slice(0, recorded.indexOf("\n\n") + 2)));
            },
          }),
        ),
    );
    const controller = new AbortController();
    const events: AskEvent[] = [];
    const asked = createAskTransport(fetchFn).ask("x", undefined, (e) => {
      events.push(e);
      controller.abort(new Error("user stopped"));
    }, controller.signal);
    await expect(asked).rejects.toThrow("user stopped");
    expect(calls[0].init.signal).toBe(controller.signal);
    expect(events.map((e) => e.type)).toEqual(["start"]);
  });
});
