// The sources rail (TKT-138): unwired source nodes stack left of the other
// nodes, their saved layout is ignored, autolayout leaves them out, and a
// toggle returns them. A wired source sits in the flow (TKT-286).

import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { autoLayout, tidyLayout } from "../src/autoLayout.js";
import { makeCanvasUpdate } from "../src/canvasIntents.js";
import { sourceMenuItems } from "../src/sourceMenu.js";
import { DEFAULT_SOURCE_VIEW, isSourceDescriptor, NO_SOURCE_VIEW, railSources, type SourceView } from "../src/sources.js";
import { buildCanvasModel } from "../src/viewModel.js";

const duckSource: NodeDescriptor = {
  kind: "duck.source", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "" }],
};
const valuePath: NodeDescriptor = {
  kind: "value.path", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "path", type: "Text", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "" }],
};
const query: NodeDescriptor = {
  kind: "duck.query", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", bindable: "Database" }],
};
const tableSource: NodeDescriptor = {
  kind: "t.load", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }], params: [],
};
const catalog = new Map([duckSource, valuePath, query, tableSource].map((d) => [d.kind, d]));

function graph() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
  store.dispatch({ type: "addNode", id: "ifc", kind: "value.path", version: 1 });
  store.dispatch({ type: "addNode", id: "q1", kind: "duck.query", version: 1 });
  store.dispatch({ type: "addNode", id: "q2", kind: "duck.query", version: 1 });
  store.dispatch({ type: "addNode", id: "t", kind: "t.load", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "db", layout: { x: 500, y: 500 } });
  store.dispatch({ type: "setLayout", nodeId: "ifc", layout: { x: 600, y: 40 } });
  store.dispatch({ type: "setLayout", nodeId: "q1", layout: { x: 300, y: 100 } });
  store.dispatch({ type: "setLayout", nodeId: "q2", layout: { x: 300, y: 400 } });
  store.dispatch({ type: "setLayout", nodeId: "t", layout: { x: 700, y: 100 } });
  return store;
}
const build = (store: ReturnType<typeof graph>, sources: SourceView) =>
  buildCanvasModel(store.getState(), catalog, null, undefined, { sources });

describe("source nodes", () => {
  it("are nodes with no inputs and no Table output", () => {
    expect([duckSource, valuePath, query, tableSource].map(isSourceDescriptor)).toEqual([true, true, false, false]);
    expect(isSourceDescriptor(undefined)).toBe(false);
  });
});

describe("the sources rail", () => {
  it("stacks source nodes in one column left of the others, ignoring their saved layout", () => {
    const model = build(graph(), DEFAULT_SOURCE_VIEW);
    const [db, ifc, ...others] = ["db", "ifc", "q1", "q2", "t"].map((id) => model.nodes.find((n) => n.id === id)!);
    expect([db!.inRail, ifc!.inRail]).toEqual([true, true]);
    expect(others.map((n) => n.inRail)).toEqual([undefined, undefined, undefined]);
    expect(db!.x).toBe(ifc!.x);
    expect(ifc!.y).toBeGreaterThanOrEqual(db!.y + db!.h);
    for (const n of others) expect(db!.x + db!.w).toBeLessThan(n.x);
    expect(db!.y).toBe(100);
  });

  it("draws every node where it was saved with the rail off, and by default", () => {
    for (const view of [NO_SOURCE_VIEW]) {
      const model = build(graph(), view);
      expect(model.nodes.some((n) => n.inRail)).toBe(false);
      expect(model.nodes.find((n) => n.id === "db")).toMatchObject({ x: 500, y: 500 });
    }
    const plain = buildCanvasModel(graph().getState(), catalog);
    expect(plain.nodes.find((n) => n.id === "ifc")).toMatchObject({ x: 600, y: 40 });
  });

  it("does not let a source take a free spot among the others", () => {
    const store = graph();
    store.dispatch({ type: "setLayout", nodeId: "db", layout: { x: 80, y: 80 } });
    store.dispatch({ type: "addNode", id: "late", kind: "t.load", version: 1 });
    const railed = build(store, DEFAULT_SOURCE_VIEW).nodes.find((n) => n.id === "late")!;
    const flat = build(store, NO_SOURCE_VIEW).nodes.find((n) => n.id === "late")!;
    expect({ x: flat.x, y: flat.y }).not.toEqual({ x: railed.x, y: railed.y });
  });

  it("is left out of autolayout and tidy layout", () => {
    const model = build(graph(), DEFAULT_SOURCE_VIEW);
    expect(Object.keys(autoLayout(model, { width: 1200, height: 800 })).sort()).toEqual(["q1", "q2", "t"]);
    expect(Object.keys(tidyLayout(model)).sort()).toEqual(["q1", "q2", "t"]);
  });

  it("cannot be moved or resized", () => {
    const store = graph();
    const update = makeCanvasUpdate(store, () => {});
    const doc = build(store, DEFAULT_SOURCE_VIEW);
    expect(update(doc, { kind: "move", id: "db", x: 1, y: 1 })).toBe(doc);
    expect(update(doc, { kind: "resize", id: "db", w: 400, h: 200 })).toBe(doc);
    update(doc, { kind: "moveEnd", id: "db" });
    update(doc, { kind: "resizeEnd", id: "db" });
    expect(store.getState().document.layout["db"]).toEqual({ x: 500, y: 500 });
    expect(update(doc, { kind: "move", id: "q1", x: 1, y: 1 })).not.toBe(doc);
  });
});

describe("the rail toggle", () => {
  const menu = (view: SourceView) => {
    const calls: SourceView[] = [];
    const items = (id: string) => sourceMenuItems({
      view: () => view, setView: (v) => calls.push(v), isSource: (n) => n === "db", rowAt: () => null, promote: () => {},
    }, id, 0, 0);
    return { calls, items };
  };

  it("is offered on a source node only, and flips the rail", () => {
    const { calls, items } = menu(DEFAULT_SOURCE_VIEW);
    expect(items("q1")).toEqual([]);
    expect(items("db").map((i) => i.label)).toEqual(["Show sources on the canvas"]);
    items("db")[0]!.run();
    expect(calls).toEqual([{ rail: false }]);
  });

  it("offers the way back when the rail is off", () => {
    const { calls, items } = menu({ rail: false });
    expect(items("db").map((i) => i.label)).toEqual(["Move sources to the rail"]);
    items("db")[0]!.run();
    expect(calls).toEqual([{ rail: true }]);
  });
});

describe("which sources stand in the rail (TKT-286)", () => {
  const reader: NodeDescriptor = {
    kind: "r.read", version: 1, capability: "Pure", description: "",
    inputs: [{ name: "rel", type: "Database", optional: false }],
    outputs: [{ name: "table", type: "Table", optional: false }],
    params: [{ name: "path", kind: "FilePath", default: "", bindable: "Database" }],
  };
  const withReader = new Map([...catalog, [reader.kind, reader]]);
  const store = () => {
    const s = graph();
    s.dispatch({ type: "addNode", id: "r", kind: "r.read", version: 1 });
    s.dispatch({ type: "connect", from: "db.source", to: "q1.path" });
    s.dispatch({ type: "connect", from: "ifc.path", to: "r.rel" });
    return s;
  };

  it("leaves every wired source in the flow, with its wires drawn, and keeps an unwired one in the rail", () => {
    const s = store();
    s.dispatch({ type: "addNode", id: "spare", kind: "duck.source", version: 1 });
    const model = buildCanvasModel(s.getState(), withReader, null, undefined, { sources: DEFAULT_SOURCE_VIEW });
    const rail = (id: string) => model.nodes.find((n) => n.id === id)!.inRail ?? false;
    expect([rail("db"), rail("ifc"), rail("spare")]).toEqual([false, false, true]);
    expect(model.edges.map((e) => e.from).sort()).toEqual(["db.source", "ifc.path"]);
    expect([...railSources(s.getState().document, new Set(["db", "ifc", "spare"]))]).toEqual(["spare"]);
  });
});
