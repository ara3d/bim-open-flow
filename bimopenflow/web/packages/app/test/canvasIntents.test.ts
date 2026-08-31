import { describe, expect, it } from "vitest";
import { createStore } from "@bimopenflow/state";
import {
  anchorId,
  canConnect,
  makeCanvasUpdate,
  parseAnchorId,
} from "../src/canvasIntents.js";
import { buildCanvasModel, edgeId } from "../src/viewModel.js";

const emptyCatalog = new Map();

function setup() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: "k", version: 1 });
  const errors: string[] = [];
  const update = makeCanvasUpdate(store, (m) => errors.push(m));
  const model = () => buildCanvasModel(store.getState(), emptyCatalog);
  return { store, update, errors, model };
}

describe("anchor ids", () => {
  it("round-trip through parseAnchorId", () => {
    expect(parseAnchorId(anchorId("out", "n1", "result"))).toEqual({
      dir: "out",
      nodeId: "n1",
      port: "result",
      endpoint: "n1.result",
    });
  });
});

describe("canConnect", () => {
  const out = { dir: "out" as const, nodeId: "a", type: "Table" as const };
  it("connects out to in across nodes with matching types", () => {
    expect(canConnect(out, { dir: "in", nodeId: "b", type: "Table" })).toBe(true);
  });
  it("rejects same direction, same node, and mismatched types", () => {
    expect(canConnect(out, { dir: "out", nodeId: "b", type: "Table" })).toBe(false);
    expect(canConnect(out, { dir: "in", nodeId: "a", type: "Table" })).toBe(false);
    expect(canConnect(out, { dir: "in", nodeId: "b", type: "Number" })).toBe(false);
  });
  it("treats Any as a wildcard", () => {
    expect(canConnect(out, { dir: "in", nodeId: "b", type: "Any" })).toBe(true);
  });
});

describe("makeCanvasUpdate", () => {
  it("keeps moves transient and commits the position on moveEnd", () => {
    const { store, update, model } = setup();
    let doc = model();
    doc = update(doc, { kind: "move", id: "a", x: 500, y: 60 });
    expect(store.getState().document.layout["a"]).toBeUndefined();
    update(doc, { kind: "moveEnd", id: "a" });
    expect(store.getState().document.layout["a"]).toEqual({ x: 500, y: 60 });
  });

  it("dispatches connect with the output normalized as 'from', either drag direction", () => {
    const { store, update, model } = setup();
    update(model(), { kind: "connect", a: "in:b.in", b: "out:a.out" });
    expect(store.getState().document.structure.edges).toEqual([{ from: "a.out", to: "b.in" }]);
  });

  it("reports reducer rejections instead of throwing", () => {
    const { update, errors, model } = setup();
    expect(() =>
      update(model(), { kind: "connect", a: "out:a.out", b: "in:missing" }),
    ).not.toThrow();
    expect(errors).toHaveLength(1);
  });

  it("routes sync to a full model replacement", () => {
    const { update, model } = setup();
    const fresh = model();
    expect(update(fresh, { kind: "sync", model: fresh })).toBe(fresh);
  });

  it("deletes the selected wire, then falls back to selected nodes", () => {
    const { store, update, model } = setup();
    store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
    let doc = model();
    doc = update(doc, { kind: "selectEdge", id: edgeId("a.out", "b.in") });
    doc = update(doc, { kind: "deleteSelected" });
    expect(store.getState().document.structure.edges).toEqual([]);
    update(doc, { kind: "selectNode", id: "b" });
    update(doc, { kind: "deleteSelected" });
    expect(store.getState().document.structure.nodes.map((n) => n.id)).toEqual(["a"]);
  });

  it("routes node selection to the store", () => {
    const { store, update, model } = setup();
    update(model(), { kind: "selectNode", id: "a" });
    expect(store.getState().selection).toEqual(["a"]);
    update(model(), { kind: "clearSelection" });
    expect(store.getState().selection).toEqual([]);
  });
});
