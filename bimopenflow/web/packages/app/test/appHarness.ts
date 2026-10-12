// Drives the real createApp over a fake host api in jsdom. Each test file
// that mounts the whole app calls stubCanvas() in beforeAll and
// unmountApps() in afterEach.

import type { ApiClient } from "@bimopenflow/api-client";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { HostStatusSource } from "@bimopenflow/client/host";
import { createApp, type App, type AppOptions } from "../src/app.js";

export const tableDesc: NodeDescriptor = {
  kind: "table.select",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: true }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

/** A flow document of table.select nodes wired in a chain, first to last. */
export const chainDocument = (...ids: string[]) => ({
  formatVersion: "0.1.0",
  structure: {
    nodes: ids.map((id) => ({ id, kind: "table.select", version: 1 })),
    edges: ids.slice(1).map((id, i) => ({ from: `${ids[i]}.out`, to: `${id}.in` })),
  },
  values: {},
  layout: {},
});

const okState = (nodeId: string) => ({ nodeId, status: "Ok", warnings: [] });

/** A host holding `docs` (id to document); every method it does not name resolves to {}.
 *  `overrides` replaces any method, and the returned `calls` counts each method's calls. */
export function fakeApi(
  docs: Record<string, object>,
  overrides: Partial<Record<keyof ApiClient, unknown>> = {},
): { api: ApiClient; calls: Record<string, unknown[][]> } {
  const stored = new Map(Object.entries(docs).map(([id, d]) => [id, JSON.stringify(d)]));
  const calls: Record<string, unknown[][]> = {};
  const known: Partial<Record<keyof ApiClient, unknown>> = {
    listAnalyses: async () => [...stored.keys()].sort().map((id) => ({ id, graphHash: `hash-${stored.get(id)!.length}` })),
    listModels: async () => [],
    getNodeCatalog: async () => ({ nodes: [tableDesc] }),
    getAnalysis: async (id: string) => {
      const json = stored.get(id);
      if (json === undefined) throw new Error(`404 ${id}`);
      return json;
    },
    getAnalysisState: async (id: string) => ({
      analysisId: id,
      nodes: (JSON.parse(stored.get(id) ?? "{}").structure?.nodes ?? []).map((n: { id: string }) => okState(n.id)),
    }),
    analysisEvents: () => () => {},
    putAnalysis: async (id: string, body: string) => { stored.set(id, body); return { id, graphHash: "" }; },
    putSession: async (body: unknown) => body,
    getSession: async () => ({ selection: [] }),
    getResult: () => new Promise(() => {}),
    getSuggestions: async () => [],
    ...overrides,
  };
  const api = new Proxy(known, {
    get: (target, name: string) => {
      const f = (target[name as keyof ApiClient] ?? (async () => ({}))) as (...args: unknown[]) => unknown;
      return (...args: unknown[]) => { (calls[name] ??= []).push(args); return f(...args); };
    },
  }) as unknown as ApiClient;
  return { api, calls };
}

/** Lets pending promises and zero-delay timers run. */
export const settle = async () => {
  for (let i = 0; i < 10; i++) await new Promise((r) => setTimeout(r, 0));
};

/** jsdom has no ResizeObserver and no 2D canvas; the canvas runtime needs both to mount, not to draw here. */
export function stubCanvas(): void {
  globalThis.ResizeObserver ??= class { observe() {} unobserve() {} disconnect() {} } as never;
  // Any property read is a no-op method; text measures as 7 px per character.
  const context = new Proxy({} as Record<string | symbol, unknown>, {
    get: (target, name) => name in target ? target[name]
      : name === "measureText" ? (s: string) => ({ width: s.length * 7 })
      : () => ({ addColorStop: () => {} }),
    set: (target, name, value) => { target[name] = value; return true; },
  });
  HTMLCanvasElement.prototype.getContext = (() => context) as never;
}

/** A host that is always connected, so the app boots at once and never probes. */
export const connectedHost: HostStatusSource = {
  fetch: globalThis.fetch,
  get: () => ({ status: "connected" }) as never,
  subscribe: () => () => {},
  reportFailure: () => {},
  reportSuccess: () => {},
  start: () => {},
  dispose: () => {},
};

const mounted: { app: App; root: HTMLElement }[] = [];

/** Mounts createApp on a fresh element in the document; unmountApps() removes it. */
export function mountApp(api: ApiClient, options: AppOptions = {}): { app: App; root: HTMLElement } {
  const root = window.document.createElement("div");
  window.document.body.append(root);
  const app = createApp(root, api, { host: connectedHost, ...options });
  mounted.push({ app, root });
  return { app, root };
}

export function unmountApps(): void {
  for (const { app, root } of mounted.splice(0)) {
    app.dispose();
    root.remove();
  }
}
