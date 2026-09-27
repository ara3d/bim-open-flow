import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
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

/** A catalog whose "k" nodes carry one Json param, "note" — a real long-text
 *  row (slotControl(note) === "longText") — so tests can open a long-value
 *  editor on a real parameter. */
const kWithParam: NodeDescriptor = {
  kind: "k",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [],
  params: [{ name: "note", kind: "Json", default: "{}" }],
  description: "",
};
const paramCatalog = new Map([["k", kWithParam]]);

/** Same node and parameter name, but "note" is now a short plain Text field:
 *  a live catalog reload or descriptor change that no longer draws a
 *  long-text row for it. */
const kWithFieldParam: NodeDescriptor = {
  ...kWithParam,
  params: [{ name: "note", kind: "Text", default: "hi" }],
};
const fieldParamCatalog = new Map([["k", kWithFieldParam]]);

function setupWithParam() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
  const errors: string[] = [];
  const update = makeCanvasUpdate(store, (m) => errors.push(m));
  const model = () => buildCanvasModel(store.getState(), paramCatalog);
  const fieldModel = () => buildCanvasModel(store.getState(), fieldParamCatalog);
  return { store, update, errors, model, fieldModel };
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

  it("opens the long-value editor without touching the store", () => {
    const { store, update, model } = setup();
    const before = store.getState();
    const doc = update(model(), { kind: "openEditor", nodeId: "a", name: "note" });
    expect(doc.openEditor).toEqual({ nodeId: "a", name: "note" });
    expect(store.getState()).toBe(before);
  });

  it("closes the long-value editor", () => {
    const { update, model } = setup();
    let doc = update(model(), { kind: "openEditor", nodeId: "a", name: "note" });
    doc = update(doc, { kind: "closeEditor" });
    expect(doc.openEditor).toBeNull();
  });

  it("keeps an open editor across sync when its node and parameter survive", () => {
    const { update, model } = setupWithParam();
    let doc = update(model(), { kind: "openEditor", nodeId: "a", name: "note" });
    const next = model();
    doc = update(doc, { kind: "sync", model: next });
    expect(doc.openEditor).toEqual({ nodeId: "a", name: "note" });
    expect(doc).not.toBe(next);
  });

  it("closes the editor on sync once removeNode drops its node", () => {
    const { store, update, model } = setupWithParam();
    let doc = update(model(), { kind: "openEditor", nodeId: "a", name: "note" });
    store.dispatch({ type: "removeNode", id: "a" });
    doc = update(doc, { kind: "sync", model: model() });
    expect(doc.openEditor).toBeNull();
  });

  it("closes the editor on sync once its row stops being long text", () => {
    // Regression for design note 1: the row and its node can both still
    // exist (a catalog reload changes only the descriptor), and the old
    // check kept the editor open under a row that no longer renders it.
    const { update, model, fieldModel } = setupWithParam();
    let doc = update(model(), { kind: "openEditor", nodeId: "a", name: "note" });
    doc = update(doc, { kind: "sync", model: fieldModel() });
    expect(doc.openEditor).toBeNull();
  });

  it("keeps a view.note's editor open across sync, though it has no params to find", () => {
    // A view.note has no CanvasParam ("text" is drawn as the whole card, not
    // a row), so the ordinary param+slotControl check would always miss and
    // close the editor on the very next sync.
    const noteDesc: NodeDescriptor = {
      kind: "view.note", version: 1, capability: "Pure",
      inputs: [], outputs: [], params: [{ name: "text", kind: "Text", default: "" }], description: "",
    };
    const catalog = new Map([["view.note", noteDesc]]);
    const store = createStore();
    store.dispatch({ type: "addNode", id: "n1", kind: "view.note", version: 1 });
    const update = makeCanvasUpdate(store, () => {});
    const model = () => buildCanvasModel(store.getState(), catalog);

    let doc = update(model(), { kind: "openEditor", nodeId: "n1", name: "text" });
    doc = update(doc, { kind: "sync", model: model() });
    expect(doc.openEditor).toEqual({ nodeId: "n1", name: "text" });
  });
});
