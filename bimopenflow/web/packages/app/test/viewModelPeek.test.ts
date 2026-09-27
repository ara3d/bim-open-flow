import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { buildCanvasModel } from "../src/viewModel.js";
import { NO_PORT_RESULTS, type PortResultsView } from "../src/portResults.js";

const desc = (kind: string): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
});

const catalog = new Map([["k.a", desc("k.a")], ["k.b", desc("k.b")]]);

function wiredStore() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: "k.b", version: 1 });
  store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
  return store;
}

describe("buildCanvasModel peek and row counts", () => {
  it("gives an edge rows looked up by its from endpoint", () => {
    const store = wiredStore();
    store.dispatch({ type: "markSaved" });
    const results: PortResultsView = {
      counts: new Map([["a.out", { rows: 2, current: true }]]),
      peek: null,
    };
    const model = buildCanvasModel(store.getState(), catalog, null, results);
    expect(model.edges[0]).toMatchObject({ from: "a.out", rows: { rows: 2, current: true } });
  });

  it("forces current false while the document is dirty", () => {
    const store = wiredStore();
    expect(store.getState().dirty).toBe(true); // dispatching addNode/addEdge marks the document dirty
    const results: PortResultsView = {
      counts: new Map([["a.out", { rows: 2, current: true }]]),
      peek: null,
    };
    const model = buildCanvasModel(store.getState(), catalog, null, results);
    expect(model.edges[0]!.rows).toEqual({ rows: 2, current: false });
  });

  it("passes the peek through unchanged", () => {
    const store = wiredStore();
    const results: PortResultsView = {
      counts: new Map(),
      peek: { endpoint: "a.out", pinned: false, peek: { kind: "loading" } },
    };
    const model = buildCanvasModel(store.getState(), catalog, null, results);
    expect(model.peek).toEqual({ endpoint: "a.out", pinned: false, peek: { kind: "loading" } });
  });

  it("gives no rows or peek when results has nothing to offer", () => {
    const store = wiredStore();
    const model = buildCanvasModel(store.getState(), catalog, null, NO_PORT_RESULTS);
    expect(model.edges[0]!.rows).toBeUndefined();
    expect(model.peek).toBeUndefined();
  });

  it("gives the same model when called without the fourth argument", () => {
    const store = wiredStore();
    const withDefault = buildCanvasModel(store.getState(), catalog, null);
    const withExplicit = buildCanvasModel(store.getState(), catalog, null, NO_PORT_RESULTS);
    expect(withDefault).toEqual(withExplicit);
  });
});
