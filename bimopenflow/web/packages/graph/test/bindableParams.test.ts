// Bindable parameters (TKT-203): a parameter that declares `bindable` is drawn
// with an input socket beside its inline control; wired, the control collapses
// to a preview; cut, it returns with its stored string.

import { describe, expect, it } from "vitest";
import { Runtime } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { anchorId, canConnect, makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { createCanvasInstance } from "../src/instance.js";
import { slotControl } from "../src/canvasSlots.js";
import { paramSockets } from "../src/portGeometry.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";

const source: NodeDescriptor = {
  kind: "duck.source", version: 1, capability: "Pure", description: "",
  inputs: [],
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "" }],
};
const query: NodeDescriptor = {
  kind: "duck.query", version: 1, capability: "Pure", description: "",
  inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: ".duckdb" }, bindable: "Database" },
    { name: "sql", kind: "Text", default: "" },
    { name: "keys", kind: "Text", default: "", list: true },
  ],
};
const catalog = new Map([[source.kind, source], [query.kind, query]]);

function setup() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
  store.dispatch({ type: "addNode", id: "q", kind: "duck.query", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "C:/models/city.duckdb" });
  store.dispatch({ type: "setParam", nodeId: "q", name: "path", value: "C:/old/stored.duckdb" });
  const errors: string[] = [];
  const update = makeCanvasUpdate(store, (m) => errors.push(m));
  const model = () => buildCanvasModel(store.getState(), catalog);
  const q = () => model().nodes.find((n) => n.id === "q")!;
  return { store, update, model, q, errors };
}

describe("bindable parameter sockets", () => {
  it("draws a socket for a bindable parameter only, typed by `bindable`", () => {
    const { q } = setup();
    const sockets = paramSockets(q());
    expect(sockets.map((s) => [s.name, s.type])).toEqual([["path", "Database"]]);
    expect(q().params.map((p) => [p.name, p.bindable, p.list])).toEqual([
      ["path", "Database", undefined], ["sql", undefined, undefined], ["keys", undefined, true],
    ]);
  });

  it("accepts a Database output, and refuses a Table output", () => {
    const socket = { dir: "in" as const, nodeId: "q", type: "Database" as const };
    expect(canConnect({ dir: "out", nodeId: "db", type: "Database" }, socket)).toBe(true);
    expect(canConnect({ dir: "out", nodeId: "other", type: "Table" }, socket)).toBe(false);
  });

  it("offers no socket for a parameter that is not bindable", () => {
    const { q } = setup();
    expect(paramSockets(q()).some((s) => s.name === "sql" || s.name === "keys")).toBe(false);
  });
});

describe("wiring a bindable parameter", () => {
  it("writes the edge to node.<param> and collapses the control to a preview", () => {
    const { store, update, model, q, errors } = setup();
    const before = q();
    expect(slotControl(before.params[0]!)).toBe("filePath");
    update(model(), { kind: "connect", a: anchorId("out", "db", "source"), b: anchorId("in", "q", "path") });
    expect(errors).toEqual([]);
    expect(store.getState().document.structure.edges).toEqual([{ from: "db.source", to: "q.path" }]);
    const after = q();
    expect(after.params[0]!.wired).toBe("db.source · city.duckdb");
    expect(slotControl(after.params[0]!)).toBe("wired");
    expect(after.h).toBeLessThan(before.h);
    expect(after.params[1]!.wired).toBeUndefined();
  });

  it("cutting the wire restores the inline control with its stored string", () => {
    const { store, update, model, q } = setup();
    update(model(), { kind: "connect", a: anchorId("out", "db", "source"), b: anchorId("in", "q", "path") });
    const wired = model();
    const edge = wired.edges[0]!;
    update({ ...wired, selectedEdgeId: edge.id }, { kind: "deleteSelected" });
    expect(store.getState().document.structure.edges).toEqual([]);
    const param = q().params[0]!;
    expect(param.wired).toBeUndefined();
    expect(slotControl(param)).toBe("filePath");
    expect(param.value).toBe("C:/old/stored.duckdb");
  });

  it("ignores an edge into a parameter that is not bindable", () => {
    const { store, model, q } = setup();
    store.dispatch({ type: "connect", from: "db.source", to: "q.sql" });
    expect(model().edges).toHaveLength(1);
    expect(q().params[1]!.wired).toBeUndefined();
    expect(slotControl(q().params[1]!)).toBe("field");
  });
});

describe("the mounted canvas", () => {
  it("draws the wire into the parameter's socket, wired or cut", () => {
    const { store, model } = setup();
    store.dispatch({ type: "connect", from: "db.source", to: "q.path" });
    const errors: string[] = [];
    const runtime = new Runtime<CanvasModel, CanvasIntent>(
      null,
      {
        init: model(),
        update: makeCanvasUpdate(store, (m) => errors.push(m)),
        view: (doc) => canvasView(doc, createCanvasInstance({ document })),
      },
      { headless: true, width: 800, height: 600 },
    );
    runtime.step(3, 1 / 60);
    store.dispatch({ type: "disconnect", from: "db.source", to: "q.path" });
    runtime.dispatch({ kind: "sync", model: model() });
    runtime.step(3, 1 / 60);
    expect(errors).toEqual([]);
    expect(runtime.doc.nodes.find((n) => n.id === "q")!.params[0]!.wired).toBeUndefined();
  });
});
