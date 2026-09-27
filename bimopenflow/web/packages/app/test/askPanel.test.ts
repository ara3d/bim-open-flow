// TKT-84: the editor's Ask panel mounts only when the host answers
// /api/ask/model, and edits the OPEN flow rather than a freshly named one.
import { describe, expect, it, vi } from "vitest";
import { mountAskPanel } from "../src/askPanel.js";
import type { HostStatusSource } from "../src/hostStatus.js";

/** A minimal HostStatusSource whose fetch is the one under test; the panel
 *  never reads any other member. */
function hostWith(fetchFn: typeof fetch): HostStatusSource {
  return {
    fetch: fetchFn,
    get: () => ({ status: "connected", failures: 0 }),
    subscribe: () => () => {},
    reportFailure: () => {},
    reportSuccess: () => {},
    start: () => {},
    dispose: () => {},
  };
}

/** A `Response` that streams the given SSE frames. */
function sseResponse(frames: string[]): Response {
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const frame of frames) controller.enqueue(encoder.encode(frame));
      controller.close();
    },
  });
  return new Response(body, { status: 200 });
}

describe("mountAskPanel: feature detection", () => {
  it("mounts nothing when the host has no /api/ask (a 404)", async () => {
    const root = document.createElement("div");
    const fetchFn = (async () => new Response("not found", { status: 404 })) as typeof fetch;
    const panel = await mountAskPanel(root, {
      host: hostWith(fetchFn),
      getAnalysisId: () => "a1",
      onBuilt: () => {},
      onError: () => {},
    });
    expect(root.children).toHaveLength(0);
    expect(() => panel.dispose()).not.toThrow();
  });

  it("mounts the panel when the host answers /api/ask/model", async () => {
    const info = { model: "claude-haiku", provider: "claude-cli", configured: true, problem: null };
    const fetchFn = (async () => new Response(JSON.stringify(info), { status: 200 })) as typeof fetch;
    const root = document.createElement("div");
    const panel = await mountAskPanel(root, {
      host: hostWith(fetchFn),
      getAnalysisId: () => "a1",
      onBuilt: () => {},
      onError: () => {},
    });
    expect(root.querySelector(".bof-ask-panel")).not.toBeNull();
    expect(root.querySelector("input[aria-label='Ask for an edit to the open flow']")).not.toBeNull();
    panel.dispose();
    expect(root.children).toHaveLength(0);
  });

  it("shows the configuration problem in the transcript when no provider is set up", async () => {
    const info = { model: "", configured: false, problem: "No model available." };
    const fetchFn = (async () => new Response(JSON.stringify(info), { status: 200 })) as typeof fetch;
    const root = document.createElement("div");
    await mountAskPanel(root, { host: hostWith(fetchFn), getAnalysisId: () => "a1", onBuilt: () => {}, onError: () => {} });
    const line = root.querySelector(".bof-ask-bad");
    expect(line?.textContent).toBe("No model available.");
  });
});

describe("mountAskPanel: sending a request", () => {
  async function mount(fetchFn: typeof fetch, getAnalysisId: () => string | undefined, onBuilt = vi.fn(), onError = vi.fn()) {
    const info = { model: "claude-haiku", configured: true, problem: null };
    const modelFetch: typeof fetch = (async (url: any, init: any) => {
      if (String(url).endsWith("/api/ask/model")) return new Response(JSON.stringify(info), { status: 200 });
      return fetchFn(url, init);
    }) as typeof fetch;
    const root = document.createElement("div");
    document.body.append(root);
    const panel = await mountAskPanel(root, { host: hostWith(modelFetch), getAnalysisId, onBuilt, onError });
    return { root, panel, onBuilt, onError };
  }

  it("reports an error instead of posting when no flow is open", async () => {
    const post = vi.fn();
    const { root, onError } = await mount(post as unknown as typeof fetch, () => undefined);
    const input = root.querySelector<HTMLInputElement>("input")!;
    input.value = "add a filter";
    root.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
    await Promise.resolve();
    expect(onError).toHaveBeenCalledWith("Open a flow first.");
    expect(post).not.toHaveBeenCalled();
  });

  it("posts the open flow's id and streams the transcript", async () => {
    let sentBody: unknown;
    const fetchFn = (async (_url: any, init: any) => {
      sentBody = JSON.parse(String(init.body));
      return sseResponse([
        'data: {"type":"start","analysisId":"snowdon-doors","model":"claude-haiku"}\n\n',
        'data: {"type":"tool","name":"setParam","args":{"id":"snowdon-doors","value":"x"},"ok":true}\n\n',
        'data: {"type":"done","built":true,"analysisId":"snowdon-doors","verified":true,"turns":2,"text":"Added the filter."}\n\n',
      ]);
    }) as typeof fetch;
    const { root, onBuilt } = await mount(fetchFn, () => "snowdon-doors");
    const input = root.querySelector<HTMLInputElement>("input")!;
    input.value = "add a filter for width < 850";
    root.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
    // The submit handler's async work runs after the microtask queue drains.
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(sentBody).toEqual({ request: "add a filter for width < 850", analysisId: "snowdon-doors" });
    expect(root.querySelector(".bof-ask-you")?.textContent).toBe("You: add a filter for width < 850");
    expect(root.textContent).toContain("setParam");
    expect(root.textContent).toContain("Added the filter.");
    expect(onBuilt).toHaveBeenCalledWith("snowdon-doors");
  });
});
