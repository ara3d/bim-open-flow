// TKT-113: the studio's Preview node picker is an explicit choice, like a
// double-click on the canvas, so the pane area shows the node picked there
// instead of staying on the flow's answer. Drives the real createApp over a
// fake host api in jsdom.

import { afterEach, beforeAll, describe, expect, it } from "vitest";
import type { ApiClient } from "@bimopenflow/api-client";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createApp, type App } from "../src/app.js";
import type { HostStatusSource } from "@bimopenflow/client/host";

const tableDesc: NodeDescriptor = {
  kind: "table.select",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: true }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

// source -> mid -> answer: "answer" is the only terminal, so it is the flow's answer.
const document = {
  formatVersion: "0.1.0",
  structure: {
    nodes: ["source", "mid", "answer"].map((id) => ({ id, kind: "table.select", version: 1 })),
    edges: [{ from: "source.out", to: "mid.in" }, { from: "mid.out", to: "answer.in" }],
  },
  values: {},
  layout: {},
};

const okState = (nodeId: string) => ({ nodeId, status: "Ok", warnings: [] });

function fakeApi(): ApiClient {
  const known: Partial<Record<keyof ApiClient, unknown>> = {
    listAnalyses: async () => [{ id: "flow", name: "flow" }],
    listModels: async () => [],
    getNodeCatalog: async () => ({ nodes: [tableDesc] }),
    getAnalysis: async () => JSON.stringify(document),
    getAnalysisState: async () => ({ analysisId: "flow", nodes: ["source", "mid", "answer"].map(okState) }),
    analysisEvents: () => () => {},
    putAnalysis: async () => {},
    putSession: async (body: unknown) => body,
    getResult: () => new Promise(() => {}),
    getSuggestions: async () => [],
  };
  return new Proxy(known, {
    get: (target, name: string) => target[name as keyof ApiClient] ?? (async () => ({})),
  }) as unknown as ApiClient;
}

const settle = async () => {
  for (let i = 0; i < 10; i++) await new Promise((r) => setTimeout(r, 0));
};

// jsdom has no ResizeObserver and no 2D canvas; the canvas runtime needs both to mount, not to draw here.
beforeAll(() => {
  globalThis.ResizeObserver ??= class { observe() {} unobserve() {} disconnect() {} } as never;
  // Any property read is a no-op method; text measures as 7 px per character.
  const context = new Proxy({} as Record<string | symbol, unknown>, {
    get: (target, name) => name in target ? target[name]
      : name === "measureText" ? (s: string) => ({ width: s.length * 7 })
      : () => ({ addColorStop: () => {} }),
    set: (target, name, value) => { target[name] = value; return true; },
  });
  HTMLCanvasElement.prototype.getContext = (() => context) as never;
});

const host: HostStatusSource = {
  fetch: globalThis.fetch,
  get: () => ({ status: "ok" }) as never,
  subscribe: () => () => {},
  reportFailure: () => {},
  reportSuccess: () => {},
  start: () => {},
  dispose: () => {},
};

let app: App | null = null;
let root: HTMLElement | null = null;
afterEach(() => {
  app?.dispose();
  root?.remove();
  app = root = null;
});

async function openFlow(): Promise<HTMLElement> {
  root = window.document.createElement("div");
  window.document.body.append(root);
  app = createApp(root, fakeApi(), { graphDemo: true, host });
  await app.openAnalysis("flow");
  await settle();
  return root;
}

const picker = (el: HTMLElement) => el.querySelector<HTMLSelectElement>('select[aria-label="Preview node"]')!;
const header = (el: HTMLElement) => el.querySelector(".bof-app-preview-source")!.textContent ?? "";
const backButton = (el: HTMLElement) => el.querySelector<HTMLButtonElement>(".bof-app-back-to-answer")!;

function pick(el: HTMLElement, nodeId: string): void {
  const select = picker(el);
  select.value = nodeId;
  select.dispatchEvent(new Event("change"));
}

describe("Preview node picker (TKT-113)", () => {
  it("opens on the flow's answer", async () => {
    const el = await openFlow();
    expect(picker(el).value).toBe("answer");
    expect(header(el)).toMatch(/^Answer: .*\(answer\)/);
  });

  it("shows a node other than the answer once picked", async () => {
    const el = await openFlow();
    pick(el, "mid");
    await settle();
    expect(picker(el).value).toBe("mid");
    expect(header(el)).toMatch(/^Showing .*\(mid\)/);
    expect(backButton(el).hidden).toBe(false);
  });

  it("picking the answer again follows the answer", async () => {
    const el = await openFlow();
    pick(el, "source");
    await settle();
    pick(el, "answer");
    await settle();
    expect(picker(el).value).toBe("answer");
    expect(header(el)).toMatch(/^Answer: .*\(answer\)/);
    expect(backButton(el).hidden).toBe(true);
  });
});
