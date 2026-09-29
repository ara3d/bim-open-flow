import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import {
  buildCanvasModel,
  edgeId,
  NODE_HEADER,
  nodeHeight,
  noteHeight,
  NOTE_KIND,
  NOTE_WIDTH,
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

  it("puts nodes without layout on free spots at their real size, clear of placed ones", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "placed", kind: "k.a", version: 1 });
    store.dispatch({ type: "setLayout", nodeId: "placed", layout: { x: 80, y: 80, w: 700 } });
    for (const id of ["a_node_with_an_identifier_long_enough_to_widen_its_card", "b", "c"])
      store.dispatch({ type: "addNode", id, kind: "k.b", version: 1 });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(buildCanvasModel(store.getState(), catalog).nodes.map(({ x, y }) => ({ x, y })))
      .toEqual(model.nodes.map(({ x, y }) => ({ x, y })));
    const boxes = model.nodes;
    for (const [i, p] of boxes.entries())
      for (const q of boxes.slice(i + 1))
        expect(p.x < q.x + q.w && q.x < p.x + p.w && p.y < q.y + q.h && q.y < p.y + p.h, `${p.id} / ${q.id}`).toBe(false);
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
    expect(model.edges).toEqual([{ id: edgeId("a.out", "b.in"), from: "a.out", to: "b.in", contributing: true }]);
    expect(model.nodes.find((n) => n.id === "a")!.status).toBe("Error");
    expect(model.nodes.find((n) => n.id === "b")!.selected).toBe(true);
    expect(model.nodes.find((n) => n.id === "a")!.selected).toBe(false);
  });

  it("draws a view.note with no ports or params, at the fixed note width, holding its text", () => {
    const noteDesc: NodeDescriptor = {
      kind: NOTE_KIND, version: 1, capability: "Pure",
      inputs: [], outputs: [], params: [{ name: "text", kind: "Text", default: "" }], description: "",
    };
    const store = createStore();
    store.dispatch({ type: "addNode", id: "n1", kind: NOTE_KIND, version: 1 });
    store.dispatch({ type: "setParam", nodeId: "n1", name: "text", value: "Check the storey mapping." });
    const model = buildCanvasModel(store.getState(), new Map([[NOTE_KIND, noteDesc]]));

    const note = model.nodes[0]!;
    expect(note.inputs).toEqual([]);
    expect(note.outputs).toEqual([]);
    expect(note.params).toEqual([]);
    expect(note.w).toBe(NOTE_WIDTH);
    expect(note.h).toBe(noteHeight("Check the storey mapping."));
    expect(note.noteText).toBe("Check the storey mapping.");
  });

  it("a view.note with no value falls back to its param's default", () => {
    const noteDesc: NodeDescriptor = {
      kind: NOTE_KIND, version: 1, capability: "Pure",
      inputs: [], outputs: [], params: [{ name: "text", kind: "Text", default: "" }], description: "",
    };
    const store = createStore();
    store.dispatch({ type: "addNode", id: "n1", kind: NOTE_KIND, version: 1 });
    const model = buildCanvasModel(store.getState(), new Map([[NOTE_KIND, noteDesc]]));
    expect(model.nodes[0]!.noteText).toBe("");
  });

  it("carries a status badge naming the eval message; nodes without eval state have none (TKT-10)", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "k.b", version: 1 });
    store.dispatch({
      type: "applyServerState",
      update: {
        analysisId: "x",
        nodes: [{ nodeId: "a", status: "Error", error: "boom", warnings: [] }],
      },
    });
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes.find((n) => n.id === "a")!.badge).toEqual({ status: "Error", text: "boom" });
    expect(model.nodes.find((n) => n.id === "b")!.badge).toBeUndefined();
  });
});
