// TKT-84 and TKT-112: the editor's Ask panel mounts only when the host answers
// /api/ask/model, and edits the OPEN flow rather than a freshly named one.
import { describe, expect, it, vi } from "vitest";
import { ASK_DEFAULT_HEIGHT, clampAskHeight, mountAskPanel } from "../src/askPanel.js";
import { buildShell, type Shell } from "../src/shell.js";
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

  // TKT-127: a question about the toolkit is answered in text; the open flow is left alone.
  it("shows a text-only answer as a reply and does not reload the open flow", async () => {
    const fetchFn = (async () => sseResponse([
      'data: {"type":"start","analysisId":"snowdon-doors","model":"claude-haiku"}\n\n',
      'data: {"type":"tool","name":"searchDocs","args":{"query":"BOS"},"ok":true}\n\n',
      'data: {"type":"done","built":false,"verified":false,"analysisId":"snowdon-doors","turns":2,"text":"BOS is BIM Open Schema (from docs/OVERVIEW.md)."}\n\n',
    ])) as typeof fetch;
    const { root, onBuilt } = await mount(fetchFn, () => "snowdon-doors");
    const input = root.querySelector<HTMLInputElement>("input")!;
    input.value = "What is BOS?";
    root.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    const reply = root.querySelector(".bof-ask-done");
    expect(reply?.textContent).toBe("Answered in 2 turns: BOS is BIM Open Schema (from docs/OVERVIEW.md).");
    expect(root.querySelector(".bof-ask-bad")).toBeNull();
    expect(onBuilt).not.toHaveBeenCalled();
  });
});

// TKT-112: the panel docks in the shell's right column with a fixed height.
describe("mountAskPanel: docked in the right column", () => {
  const info = { model: "claude-haiku", configured: true, problem: null };

  function shellFetch(frames: string[]): typeof fetch {
    return (async (url: any) => {
      if (String(url).endsWith("/api/ask/model")) return new Response(JSON.stringify(info), { status: 200 });
      return sseResponse(frames);
    }) as typeof fetch;
  }

  async function shellWithPanel(fetchFn: typeof fetch) {
    localStorage.clear();
    document.body.textContent = "";
    const root = document.createElement("div");
    document.body.append(root);
    const shell = buildShell(root);
    const panel = await mountAskPanel(shell.askHost, {
      host: hostWith(fetchFn), getAnalysisId: () => "a1", onBuilt: () => {}, onError: () => {},
    });
    return { root, shell, panel };
  }

  const rect = (el: HTMLElement) => {
    const { top, left, width, height } = el.getBoundingClientRect();
    return { top, left, width, height };
  };
  const boxes = (shell: Shell) => ({ canvas: rect(shell.canvasHost), panes: rect(shell.paneEl) });

  it("leaves the right column empty above the panes when the host has no /api/ask", async () => {
    const { shell } = await shellWithPanel((async () => new Response("", { status: 404 })) as typeof fetch);
    expect(shell.askHost.children).toHaveLength(0);
    expect(shell.askHost.nextElementSibling).toBe(shell.paneEl);
    shell.dispose();
  });

  it("sits above the pane area, not over the layout, at the default height", async () => {
    const { shell } = await shellWithPanel(shellFetch([]));
    const panelEl = shell.askHost.querySelector<HTMLElement>(".bof-ask-panel")!;
    expect(panelEl).not.toBeNull();
    expect(shell.canvasHost.querySelector(".bof-ask-panel")).toBeNull();
    expect(shell.paneEl.querySelector(".bof-ask-panel")).toBeNull();
    expect(panelEl.style.getPropertyValue("--bof-ask-height")).toBe(`${ASK_DEFAULT_HEIGHT}px`);
    shell.dispose();
  });

  it("twenty Ask events leave the canvas, the panes, and the panel height unchanged", async () => {
    const frames = Array.from({ length: 20 }, (_, i) =>
      `data: {"type":"text","text":"Line ${i} of a long streamed answer."}\n\n`);
    const { shell } = await shellWithPanel(shellFetch(frames));
    const panelEl = shell.askHost.querySelector<HTMLElement>(".bof-ask-panel")!;
    const before = { ...boxes(shell), height: panelEl.style.getPropertyValue("--bof-ask-height") };
    const input = panelEl.querySelector<HTMLInputElement>("input")!;
    input.value = "explain this flow";
    panelEl.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(panelEl.querySelectorAll(".bof-ask-agent")).toHaveLength(20);
    // jsdom has no layout engine, so the boxes are compared as reported; the
    // panel's height is the property that decides them in a real browser.
    expect({ ...boxes(shell), height: panelEl.style.getPropertyValue("--bof-ask-height") }).toEqual(before);
    shell.dispose();
  });

  it("restores a saved height and collapsed state", async () => {
    localStorage.clear();
    localStorage.setItem("bof-ask-height", "320");
    localStorage.setItem("bof-ask-collapsed", "true");
    const root = document.createElement("div");
    await mountAskPanel(root, { host: hostWith(shellFetch([])), getAnalysisId: () => "a1", onBuilt: () => {}, onError: () => {} });
    const panelEl = root.querySelector<HTMLElement>(".bof-ask-panel")!;
    expect(panelEl.style.getPropertyValue("--bof-ask-height")).toBe("320px");
    expect(panelEl.classList.contains("bof-ask-collapsed")).toBe(true);
    expect(panelEl.querySelector<HTMLElement>(".bof-ask-log")!.hidden).toBe(true);
    expect(panelEl.querySelector("input")).not.toBeNull();
    localStorage.clear();
  });
});

describe("clampAskHeight", () => {
  it("keeps room for the panes below", () => {
    expect(clampAskHeight(900, 800)).toBe(560);
    expect(clampAskHeight(10, 800)).toBe(96);
    expect(clampAskHeight(200, 800)).toBe(200);
  });
  it("applies only the lower bound before the column is laid out", () => {
    expect(clampAskHeight(900, 0)).toBe(900);
  });
});
