import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import {
  buildCanvasModel,
  defaultPosition,
  edgeId,
  NODE_HEADER,
  nodeHeight,
  PORT_SPACING,
} from "../src/viewModel.js";

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

describe("nodeHeight", () => {
  it("is header plus one port row per densest side, min one row", () => {
    expect(nodeHeight(0, 0)).toBe(NODE_HEADER + PORT_SPACING);
    expect(nodeHeight(3, 1)).toBe(NODE_HEADER + 3 * PORT_SPACING);
    expect(nodeHeight(1, 2)).toBe(NODE_HEADER + 2 * PORT_SPACING);
  });

  it("header fits the 13px id and 10px kind lines stacked", () => {
    expect(NODE_HEADER).toBeGreaterThanOrEqual(32);
    expect(PORT_SPACING).toBeGreaterThanOrEqual(16);
  });
});

describe("buildCanvasModel", () => {
  it("reads positions from the layout layer, written through actions", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 300, y: 40 } });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes[0]).toMatchObject({ id: "a", x: 300, y: 40 });
  });

  it("round-trips layout through undo/redo", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 10, y: 20 } });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 99, y: 99 } });
    store.dispatch({ type: "undo" });
    expect(buildCanvasModel(store.getState(), catalog).nodes[0]).toMatchObject({ x: 10, y: 20 });
    store.dispatch({ type: "redo" });
    expect(buildCanvasModel(store.getState(), catalog).nodes[0]).toMatchObject({ x: 99, y: 99 });
  });

  it("gives nodes without layout deterministic default positions", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "k.b", version: 1 });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes[0]).toMatchObject(defaultPosition(0));
    expect(model.nodes[1]).toMatchObject(defaultPosition(1));
    expect(defaultPosition(0)).not.toEqual(defaultPosition(1));
  });

  it("carries ports from the catalog and degrades unknown kinds to portless", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "x", kind: "unknown.kind", version: 1 });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes[0]!.inputs).toEqual([{ name: "in", type: "Table" }]);
    expect(model.nodes[1]!.inputs).toEqual([]);
    expect(model.nodes[1]!.h).toBe(nodeHeight(0, 0));
  });

  it("maps edges, selection, and eval status", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "k.b", version: 1 });
    store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
    store.dispatch({ type: "select", ids: ["b"] });
    store.dispatch({
      type: "applyServerState",
      update: {
        analysisId: "x",
        nodes: [{ nodeId: "a", status: "Error", error: "boom", warnings: [] }],
      },
    });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.edges).toEqual([{ id: edgeId("a.out", "b.in"), from: "a.out", to: "b.in" }]);
    expect(model.nodes.find((n) => n.id === "a")!.status).toBe("Error");
    expect(model.nodes.find((n) => n.id === "b")!.selected).toBe(true);
    expect(model.nodes.find((n) => n.id === "a")!.selected).toBe(false);
  });
});
