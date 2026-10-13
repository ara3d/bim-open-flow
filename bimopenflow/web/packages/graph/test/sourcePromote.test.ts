// @vitest-environment jsdom
// Promote (TKT-138): "Share as source" on an inline path makes a source holding
// it, links this parameter, and offers to link every other node holding the same path.

import { afterEach, describe, expect, it, vi } from "vitest";
import { createStore } from "@bimopenflow/state";
import { rowAt } from "../src/paramRow.js";
import { showRelinkOffer } from "../src/relinkOffer.js";
import { sourceMenuItems } from "../src/sourceMenu.js";
import { installSourceGestures } from "../src/sourceGestures.js";
import { promotePlan, relinkActions, relinkTargets } from "../src/sourcePlans.js";
import { buildCanvasModel } from "../src/viewModel.js";
import { catalog, queryGraph } from "./sourceFixtures.js";

afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); });

const planOf = (store: ReturnType<typeof queryGraph>, nodeId: string, param = "path") => {
  const plan = promotePlan(store.getState(), catalog, nodeId, param);
  if ("error" in plan) throw new Error(plan.error);
  return plan;
};

describe("promotePlan", () => {
  it("adds a duck.source holding the path and wires this socket to it", () => {
    const plan = planOf(queryGraph(), "q1");
    expect(plan.actions).toEqual([
      { type: "addNode", id: "source1", kind: "duck.source", version: 1 },
      { type: "setParam", nodeId: "source1", name: "path", value: "C:/m/city.duckdb" },
      { type: "connect", from: "source1.source", to: "q1.path" },
    ]);
    expect(plan.source).toEqual({ id: "source1", endpoint: "source1.source" });
  });

  it("offers the other nodes that hold the same path, slashes alike, and no others", () => {
    expect(planOf(queryGraph(), "q1").relink).toEqual([
      { nodeId: "q2", param: "path" }, { nodeId: "q4", param: "path" },
    ]);
  });

  it("leaves out a node already wired, a node with another path, and a parameter with no socket", () => {
    const store = queryGraph();
    store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    store.dispatch({ type: "connect", from: "db.source", to: "q2.path" });
    store.dispatch({ type: "addNode", id: "plain", kind: "bim.plain", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "plain", name: "path", value: "C:/m/city.duckdb" });
    store.dispatch({ type: "addNode", id: "ifc", kind: "bim.read", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "ifc", name: "path", value: "C:/m/city.duckdb" });
    expect(relinkTargets(store.getState(), catalog, "C:/m/city.duckdb", "path", "Database", "q1"))
      .toEqual([{ nodeId: "q4", param: "path" }]);
  });

  it("makes a value.path for a parameter that takes Text", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "r", kind: "bim.read", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "r", name: "path", value: "C:/m/house.ifc" });
    expect(planOf(store as never, "r").actions[0]).toEqual({ type: "addNode", id: "path1", kind: "value.path", version: 1 });
  });

  it("reuses a source that already holds the path", () => {
    const store = queryGraph();
    store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "C:/m/city.duckdb" });
    const plan = planOf(store, "q1");
    expect(plan.actions).toEqual([{ type: "connect", from: "db.source", to: "q1.path" }]);
  });

  it("refuses a parameter with no socket, an empty path, and one already linked", () => {
    const store = queryGraph();
    store.dispatch({ type: "addNode", id: "plain", kind: "bim.plain", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "plain", name: "path", value: "C:/m/x.ifc" });
    store.dispatch({ type: "addNode", id: "empty", kind: "duck.query", version: 1 });
    store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    store.dispatch({ type: "connect", from: "db.source", to: "q3.path" });
    const error = (id: string, param = "path") => promotePlan(store.getState(), catalog, id, param);
    expect(error("plain")).toEqual({ error: "plain path cannot take a source" });
    expect(error("q1", "sql")).toEqual({ error: "q1 sql cannot take a source" });
    expect(error("empty")).toEqual({ error: "empty path holds no path to share" });
    expect(error("q3")).toEqual({ error: "q3 path is already linked to a source" });
  });

  it("applies as one undo step, and the relink offer's actions wire every listed node", () => {
    const store = queryGraph();
    const plan = planOf(store, "q1");
    store.dispatch({ type: "batch", actions: plan.actions });
    expect(store.getState().document.structure.edges).toEqual([{ from: "source1.source", to: "q1.path" }]);
    store.dispatch({ type: "batch", actions: relinkActions(plan.source, plan.relink) });
    expect(store.getState().document.structure.edges.map((e) => e.to)).toEqual(["q1.path", "q2.path", "q4.path"]);
    expect(store.getState().document.values["q2"]).toEqual({ path: "C:/m/city.duckdb" });
    store.dispatch({ type: "undo" });
    expect(store.getState().document.structure.edges).toHaveLength(1);
    store.dispatch({ type: "undo" });
    expect(store.getState().document.structure.edges).toEqual([]);
    expect(store.getState().document.structure.nodes.map((n) => n.id)).toEqual(["q1", "q2", "q3", "q4"]);
  });
});

describe("the Share as source menu item", () => {
  const model = (store = queryGraph()) => buildCanvasModel(store.getState(), catalog);
  const pathRow = (m: ReturnType<typeof model>, id: string) => {
    const node = m.nodes.find((n) => n.id === id)!;
    for (let dy = 0; dy < node.h; dy++) {
      const hit = rowAt(m, node.x + node.w / 2, node.y + dy);
      if (hit?.param.name === "path") return { x: node.x + node.w / 2, y: node.y + dy };
    }
    throw new Error("no path row");
  };
  const items = (m: ReturnType<typeof model>, id: string, at: { x: number; y: number }) => {
    const promoted: unknown[] = [];
    const list = sourceMenuItems({
      view: () => ({ rail: true }), setView: () => {}, isSource: () => false,
      rowAt: (x, y) => rowAt(m, x, y), promote: (n, p) => promoted.push([n, p]),
    }, id, at.x, at.y);
    return { list, promoted };
  };

  it("is offered on a path row that has a socket and a path, and promotes that row", () => {
    const m = model();
    const { list, promoted } = items(m, "q1", pathRow(m, "q1"));
    expect(list.map((i) => i.label)).toEqual(["Share as source"]);
    list[0]!.run();
    expect(promoted).toEqual([["q1", "path"]]);
  });

  it("is not offered on a row that is not a path, a wired path, or an empty path", () => {
    const store = queryGraph();
    store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    store.dispatch({ type: "connect", from: "db.source", to: "q3.path" });
    store.dispatch({ type: "addNode", id: "empty", kind: "duck.query", version: 1 });
    store.dispatch({ type: "addNode", id: "plain", kind: "bim.plain", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "plain", name: "path", value: "C:/m/x.ifc" });
    const m = model(store);
    for (const id of ["q3", "empty", "plain"]) expect(items(m, id, pathRow(m, id)).list).toEqual([]);
    const q1 = m.nodes.find((n) => n.id === "q1")!;
    expect(items(m, "q1", { x: q1.x + 3, y: q1.y + 5 }).list).toEqual([]);
  });
});

describe("showRelinkOffer", () => {
  const open = (onAccept = vi.fn()) => {
    const close = showRelinkOffer({
      document, at: { x: 5, y: 6 }, source: "source1", onAccept,
      targets: [{ nodeId: "q2", param: "path" }, { nodeId: "q4", param: "path" }],
    });
    return { close, onAccept, panel: () => document.querySelector('[role="dialog"]') };
  };
  const button = (text: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === text)!;

  it("lists the nodes and links them all on one click", () => {
    const { panel, onAccept } = open();
    expect(panel()!.textContent).toContain("2 other nodes hold the same path. Link them to source1?");
    expect([...panel()!.querySelectorAll("li")].map((li) => li.textContent)).toEqual(["q2.path", "q4.path"]);
    button("Link all").click();
    expect(onAccept).toHaveBeenCalledOnce();
    expect(panel()).toBeNull();
  });

  it("closes on Not now, Escape, or a click elsewhere, without linking", () => {
    for (const close of [
      () => button("Not now").click(),
      () => document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })),
      () => document.body.dispatchEvent(new Event("pointerdown", { bubbles: true })),
    ]) {
      const { panel, onAccept } = open();
      close();
      expect(panel()).toBeNull();
      expect(onAccept).not.toHaveBeenCalled();
    }
  });
});

describe("Share as source through the editor's gestures", () => {
  it("promotes the path under the pointer, then links the others from the offer", () => {
    const store = queryGraph();
    const canvas = document.createElement("canvas");
    document.body.append(canvas);
    const errors: string[] = [];
    const gestures = installSourceGestures(canvas, {
      store, getCatalog: () => catalog, onError: (m) => errors.push(m), readOnly: false,
      getDoc: () => buildCanvasModel(store.getState(), catalog),
      canvasToWorld: (x, y) => ({ x, y }), clientToWorld: (x, y) => ({ x, y }),
      getView: () => ({ rail: true }), setView: () => {},
    });
    const m = buildCanvasModel(store.getState(), catalog);
    const q1 = m.nodes.find((n) => n.id === "q1")!;
    let at = { x: 0, y: 0 };
    for (let dy = 0; dy < q1.h; dy++) if (rowAt(m, q1.x + 40, q1.y + dy)?.param.name === "path") at = { x: q1.x + 40, y: q1.y + dy };
    const share = gestures.menuItems("q1", at.x, at.y, 100, 120).find((i) => i.label === "Share as source")!;
    share.run();
    expect(errors).toEqual([]);
    expect(store.getState().document.structure.edges).toEqual([{ from: "source1.source", to: "q1.path" }]);
    const panel = document.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(panel.style.left).toBe("100px");
    [...panel.querySelectorAll("button")].find((b) => b.textContent === "Link all")!.click();
    expect(store.getState().document.structure.edges.map((e) => e.to)).toEqual(["q1.path", "q2.path", "q4.path"]);
    gestures.dispose();
  });

  it("shows no offer when no other node holds the path", () => {
    const store = queryGraph();
    const canvas = document.createElement("canvas");
    document.body.append(canvas);
    const gestures = installSourceGestures(canvas, {
      store, getCatalog: () => catalog, onError: () => {}, readOnly: false,
      getDoc: () => buildCanvasModel(store.getState(), catalog),
      canvasToWorld: (x, y) => ({ x, y }), clientToWorld: (x, y) => ({ x, y }),
      getView: () => ({ rail: true }), setView: () => {},
    });
    const m = buildCanvasModel(store.getState(), catalog);
    const q3 = m.nodes.find((n) => n.id === "q3")!;
    let at = { x: 0, y: 0 };
    for (let dy = 0; dy < q3.h; dy++) if (rowAt(m, q3.x + 40, q3.y + dy)?.param.name === "path") at = { x: q3.x + 40, y: q3.y + dy };
    gestures.menuItems("q3", at.x, at.y, 1, 1).find((i) => i.label === "Share as source")!.run();
    expect(store.getState().document.structure.edges).toEqual([{ from: "source1.source", to: "q3.path" }]);
    expect(document.querySelector('[role="dialog"]')).toBeNull();
    gestures.dispose();
  });
});
