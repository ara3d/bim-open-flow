// @vitest-environment jsdom
// "Make a graph input" and "Inline the graph input" (TKT-204): a source's literal
// path becomes a declared input the path refers to as $name, and back. Each
// gesture is one undo step.

import { afterEach, describe, expect, it } from "vitest";
import { createStore, type Store } from "@bimopenflow/state";
import { rowAt } from "../src/chipGeometry.js";
import { inlineInputPlan, inputNameFor, makeInputPlan } from "../src/inputPlans.js";
import { installSourceGestures } from "../src/sourceGestures.js";
import { buildCanvasModel } from "../src/viewModel.js";
import { catalog } from "./sourceFixtures.js";

afterEach(() => { document.body.replaceChildren(); });

const PATH = "C:/m/city.duckdb";

function graph(): Store {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: PATH });
  store.dispatch({ type: "addNode", id: "ifc", kind: "value.path", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "ifc", name: "path", value: "C:/m/house.ifc" });
  store.dispatch({ type: "addNode", id: "q1", kind: "duck.query", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "q1", name: "path", value: PATH });
  return store;
}
const actionsOf = (plan: ReturnType<typeof makeInputPlan>) => {
  if ("error" in plan) throw new Error(plan.error);
  return plan.actions;
};
const apply = (store: Store, actions: ReturnType<typeof actionsOf>) => store.dispatch({ type: "batch", actions });
const document0 = (store: Store) => store.getState().document;

describe("makeInputPlan", () => {
  it("declares an input named after the node, holding the path, and points the path at it", () => {
    expect(actionsOf(makeInputPlan(graph().getState(), catalog, "db"))).toEqual([
      { type: "declareInput", input: { name: "db", kind: "FilePath", default: PATH } },
      { type: "setParam", nodeId: "db", name: "path", value: "$db" },
    ]);
  });

  it("does the same for a value.path source", () => {
    expect(actionsOf(makeInputPlan(graph().getState(), catalog, "ifc"))[0]).toEqual(
      { type: "declareInput", input: { name: "ifc", kind: "FilePath", default: "C:/m/house.ifc" } });
  });

  it("refuses a node that is not a source, an empty path, and a path that already refers to an input", () => {
    const store = graph();
    store.dispatch({ type: "addNode", id: "blank", kind: "duck.source", version: 1 });
    expect(makeInputPlan(store.getState(), catalog, "q1")).toEqual({ error: "q1 has no path to make a graph input of" });
    expect(makeInputPlan(store.getState(), catalog, "blank")).toEqual({ error: "blank holds no path to make a graph input of" });
    apply(store, actionsOf(makeInputPlan(store.getState(), catalog, "db")));
    expect(makeInputPlan(store.getState(), catalog, "db")).toEqual({ error: "db already takes its path from a graph input" });
  });

  it("makes a name of letters and digits that is not yet taken", () => {
    expect(inputNameFor("db", [])).toBe("db");
    expect(inputNameFor("my-db_2", [])).toBe("mydb2");
    expect(inputNameFor("2d", [])).toBe("input2d");
    expect(inputNameFor("db", ["db", "db2"])).toBe("db3");
  });

  it("is one undo step", () => {
    const store = graph();
    apply(store, actionsOf(makeInputPlan(store.getState(), catalog, "db")));
    expect(document0(store).structure.inputs).toHaveLength(1);
    expect(document0(store).values["db"]).toEqual({ path: "$db" });
    store.dispatch({ type: "undo" });
    expect(document0(store).structure.inputs).toBeUndefined();
    expect(document0(store).values["db"]).toEqual({ path: PATH });
  });
});

describe("inlineInputPlan", () => {
  const promoted = (): Store => {
    const store = graph();
    apply(store, actionsOf(makeInputPlan(store.getState(), catalog, "db")));
    return store;
  };

  it("puts the host's current value back as a literal and removes the input nobody else uses", () => {
    const store = promoted();
    store.dispatch({
      type: "applyServerState",
      update: { analysisId: "a", nodes: [], graphInputs: [{ name: "db", kind: "FilePath", default: PATH, description: "", value: "C:/m/other.duckdb" }] },
    });
    apply(store, actionsOf(inlineInputPlan(store.getState(), "db", "path")));
    expect(document0(store).values["db"]).toEqual({ path: "C:/m/other.duckdb" });
    expect(document0(store).structure.inputs).toBeUndefined();
  });

  it("falls back to the default when the host has reported no value", () => {
    const store = promoted();
    apply(store, actionsOf(inlineInputPlan(store.getState(), "db", "path")));
    expect(document0(store).values["db"]).toEqual({ path: PATH });
  });

  it("keeps the input while another parameter refers to it", () => {
    const store = promoted();
    store.dispatch({ type: "setParam", nodeId: "q1", name: "path", value: "$db" });
    apply(store, actionsOf(inlineInputPlan(store.getState(), "db", "path")));
    expect(document0(store).structure.inputs).toHaveLength(1);
    expect(document0(store).values["q1"]).toEqual({ path: "$db" });
  });

  it("is one undo step, and refuses a parameter that holds a literal", () => {
    const store = promoted();
    apply(store, actionsOf(inlineInputPlan(store.getState(), "db", "path")));
    store.dispatch({ type: "undo" });
    expect(document0(store).values["db"]).toEqual({ path: "$db" });
    expect(document0(store).structure.inputs).toHaveLength(1);
    expect(inlineInputPlan(store.getState(), "q1", "path")).toEqual({ error: "q1 path does not refer to a graph input" });
  });
});

describe("the context menu", () => {
  function menu(store: Store, nodeId: string, errors: string[] = []) {
    const canvas = document.createElement("canvas");
    document.body.append(canvas);
    const model = () => buildCanvasModel(store.getState(), catalog);
    const gestures = installSourceGestures(canvas, {
      store, getCatalog: () => catalog, onError: (m) => errors.push(m), readOnly: false,
      getDoc: model, canvasToWorld: (x, y) => ({ x, y }), clientToWorld: (x, y) => ({ x, y }),
      getView: () => ({ rail: true, chips: true }), setView: () => {}, onLit: () => {},
    });
    const node = model().nodes.find((n) => n.id === nodeId)!;
    const labels = (at = { x: node.x + 5, y: node.y + 5 }) => gestures.menuItems(nodeId, at.x, at.y, 0, 0);
    return { gestures, items: labels, node };
  }
  const labelsOf = (items: readonly { label: string }[]) => items.map((i) => i.label);

  it("offers Make a graph input on a source with a literal path, and doing it declares the input", () => {
    const store = graph();
    const errors: string[] = [];
    const { items, gestures } = menu(store, "db", errors);
    expect(labelsOf(items())).toContain("Make a graph input");
    items().find((i) => i.label === "Make a graph input")!.run();
    expect(errors).toEqual([]);
    expect(document0(store).structure.inputs).toEqual([{ name: "db", kind: "FilePath", default: PATH }]);
    expect(document0(store).values["db"]).toEqual({ path: "$db" });
    expect(labelsOf(items())).not.toContain("Make a graph input");
    expect(labelsOf(items())).toContain("Inline the graph input");
    gestures.dispose();
  });

  it("offers Inline the graph input on the source, and it undoes the promotion exactly", () => {
    const store = graph();
    const { items, gestures } = menu(store, "db");
    items().find((i) => i.label === "Make a graph input")!.run();
    items().find((i) => i.label === "Inline the graph input")!.run();
    expect(document0(store).structure.inputs).toBeUndefined();
    expect(document0(store).values["db"]).toEqual({ path: PATH });
    store.dispatch({ type: "undo" });
    expect(document0(store).values["db"]).toEqual({ path: "$db" });
    gestures.dispose();
  });

  it("offers neither on a node that is not a source and holds no reference", () => {
    const { items, gestures } = menu(graph(), "q1");
    expect(labelsOf(items())).not.toContain("Make a graph input");
    expect(labelsOf(items())).not.toContain("Inline the graph input");
    gestures.dispose();
  });

  it("offers Inline on a reader's own $name row, for that one parameter", () => {
    const store = graph();
    store.dispatch({ type: "declareInput", input: { name: "model", kind: "FilePath", default: PATH } });
    store.dispatch({ type: "setParam", nodeId: "q1", name: "path", value: "$model" });
    const { items, node, gestures } = menu(store, "q1");
    const model = buildCanvasModel(store.getState(), catalog);
    const q1 = model.nodes.find((n) => n.id === node.id)!;
    let at = { x: 0, y: 0 };
    for (let dy = 0; dy < q1.h; dy++) if (rowAt(model, q1.x + 40, q1.y + dy)?.param.name === "path") at = { x: q1.x + 40, y: q1.y + dy };
    const list = items(at);
    expect(labelsOf(list)).toContain("Inline the graph input");
    list.find((i) => i.label === "Inline the graph input")!.run();
    expect(document0(store).values["q1"]).toEqual({ path: PATH });
    expect(document0(store).structure.inputs).toBeUndefined();
    gestures.dispose();
  });
});
