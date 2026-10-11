// @vitest-environment jsdom
// Chips (TKT-138): a parameter wired to a source shows the source's name in
// place of a drawn wire; hovering lights the source and its users; pressing
// selects the source; a toggle draws the wires.

import { afterEach, describe, expect, it, vi } from "vitest";
import type { GNode } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { chipAt } from "../src/chipGeometry.js";
import { installChipHover } from "../src/chipHover.js";
import { canvasView } from "../src/canvasParts.js";
import { createCanvasInstance } from "../src/instance.js";
import { slotElement } from "../src/slotRegistry.js";
import { sourceMenuItems } from "../src/sourceMenu.js";
import { DEFAULT_SOURCE_VIEW, sourceUsers, type SourceView } from "../src/sources.js";
import { buildCanvasModel } from "../src/viewModel.js";

const duckSource: NodeDescriptor = {
  kind: "duck.source", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "" }],
};
const query: NodeDescriptor = {
  kind: "duck.query", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "path", kind: "FilePath", default: "", bindable: "Database" },
    { name: "sql", kind: "Text", default: "" },
  ],
};
const catalog = new Map([[duckSource.kind, duckSource], [query.kind, query]]);

function graph() {
  const store = createStore();
  for (const [id, kind] of [["db", "duck.source"], ["q1", "duck.query"], ["q2", "duck.query"], ["q3", "duck.query"]] as const)
    store.dispatch({ type: "addNode", id, kind, version: 1 });
  store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "C:/m/city.duckdb" });
  store.dispatch({ type: "setLayout", nodeId: "q1", layout: { x: 300, y: 100 } });
  store.dispatch({ type: "setLayout", nodeId: "q2", layout: { x: 300, y: 400 } });
  store.dispatch({ type: "setLayout", nodeId: "q3", layout: { x: 700, y: 100 } });
  store.dispatch({ type: "connect", from: "db.source", to: "q1.path" });
  store.dispatch({ type: "connect", from: "db.source", to: "q2.path" });
  return store;
}
const model = (store = graph(), sources: SourceView = DEFAULT_SOURCE_VIEW, lit: string | null = null) =>
  buildCanvasModel(store.getState(), catalog, null, undefined, { sources, lit });

afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); });

describe("chips in the view model", () => {
  it("replace the wires out of a source, and name it on the parameter row", () => {
    const m = model();
    expect(m.edges).toEqual([]);
    const row = m.nodes.find((n) => n.id === "q1")!.params.find((p) => p.name === "path")!;
    expect(row.chip).toEqual({ sourceId: "db", name: "db" });
    expect(row.wired).toBe("db.source · city.duckdb");
    expect(m.nodes.find((n) => n.id === "q3")!.params[0]!.chip).toBeUndefined();
  });

  it("leave the wires drawn when the chips are off", () => {
    const m = model(graph(), { rail: true, chips: false });
    expect(m.edges.map((e) => e.id)).toEqual(["db.source->q1.path", "db.source->q2.path"]);
    expect(m.nodes.find((n) => n.id === "q1")!.params[0]!.chip).toBeUndefined();
  });
});

describe("hovering a chip", () => {
  it("lights the source and every node that uses it", () => {
    const store = graph();
    expect(sourceUsers(store.getState().document, "db").sort()).toEqual(["q1", "q2"]);
    const lit = model(store, DEFAULT_SOURCE_VIEW, "db").nodes.filter((n) => n.lit).map((n) => n.id).sort();
    expect(lit).toEqual(["db", "q1", "q2"]);
    expect(model().nodes.some((n) => n.lit)).toBe(false);
  });

  it("finds the chip row under a world point", () => {
    const m = model();
    const q1 = m.nodes.find((n) => n.id === "q1")!;
    const x = q1.x + q1.w / 2;
    const hits = [...Array(q1.h).keys()].map((dy) => chipAt(m, x, q1.y + dy)).filter(Boolean);
    expect(hits.length).toBeGreaterThan(10);
    expect(hits[0]).toEqual({ nodeId: "q1", param: "path", sourceId: "db" });
    expect(chipAt(m, x, q1.y + 1)).toBeNull();
    expect(chipAt(m, -500, -500)).toBeNull();
  });

  it("is reported by the pointer listener once per change, and cleared on leave or a drag", () => {
    const canvas = document.createElement("canvas");
    document.body.append(canvas);
    vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({ left: 10, top: 20 } as DOMRect);
    const sourceAt = vi.fn((x: number) => (x > 100 ? "db" : null));
    const onHover = vi.fn();
    const dispose = installChipHover(canvas, { sourceAt, onHover });
    const move = (clientX: number, buttons = 0) =>
      canvas.dispatchEvent(new MouseEvent("pointermove", { clientX, clientY: 30, buttons }));
    move(50);
    expect(onHover).not.toHaveBeenCalled();
    move(200);
    move(210);
    expect(onHover.mock.calls).toEqual([["db"]]);
    expect(sourceAt).toHaveBeenLastCalledWith(200, 10);
    move(220, 1);
    expect(onHover.mock.calls).toEqual([["db"], [null]]);
    move(220);
    canvas.dispatchEvent(new MouseEvent("pointerleave"));
    expect(onHover.mock.calls.at(-1)).toEqual([null]);
    dispose();
    move(300);
    expect(onHover.mock.calls.length).toBe(4);
  });
});

describe("pressing a chip", () => {
  it("selects the source, and a plain wired row has no press", () => {
    const instance = createCanvasInstance({ document });
    const q1 = model().nodes.find((n) => n.id === "q1")!;
    const press = (chip: boolean) => {
      const param = chip ? q1.params[0]! : { ...q1.params[0]!, chip: undefined };
      const element = slotElement({ nodeId: "q1", param, instance, w: q1.w - 20, open: false });
      return (element.part.on ?? []).find((i) => i.kind === "press") as { to(node: GNode<unknown>): unknown } | undefined;
    };
    expect(press(true)!.to({ props: { param: q1.params[0] } } as GNode<unknown>)).toEqual({ kind: "selectNode", id: "db" });
    expect(press(false)).toBeUndefined();
    expect(canvasView(model(), instance)).toBeDefined();
  });
});

describe("the chip toggle", () => {
  const items = (chips: boolean, onChip: boolean) => {
    const views: unknown[] = [];
    const unlinked: unknown[] = [];
    const result = sourceMenuItems({
      view: () => ({ rail: true, chips }), setView: (v) => views.push(v), isSource: () => false,
      chipAt: () => (onChip ? { nodeId: "q1", param: "path", sourceId: "db" } : null), unlink: (h) => unlinked.push(h),
    }, "q1", 0, 0);
    return { result, views, unlinked };
  };

  it("offers wires on a chip, with an unlink", () => {
    const { result, views, unlinked } = items(true, true);
    expect(result.map((i) => i.label)).toEqual(["Unlink db", "Draw wires instead of chips"]);
    result[0]!.run();
    result[1]!.run();
    expect(unlinked).toEqual([{ nodeId: "q1", param: "path", sourceId: "db" }]);
    expect(views).toEqual([{ rail: true, chips: false }]);
  });

  it("offers nothing on a node with no chip and no source", () => {
    expect(items(true, false).result).toEqual([]);
  });
});
