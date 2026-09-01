import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import {
  buildCanvasModel,
  freePosition,
  NODE_WIDTH,
  nodeHeight,
  WIDE_NODE_WIDTH,
} from "../src/viewModel.js";

const withParams: NodeDescriptor = {
  kind: "csv.read",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "path", kind: "FilePath", default: "" },
    { name: "header", kind: "Boolean", default: "true" },
    { name: "rows", kind: "Json", default: "" },
  ],
  description: "",
};

const bare: NodeDescriptor = { ...withParams, kind: "bare.node", params: [] };

const catalog = new Map([
  ["csv.read", withParams],
  ["bare.node", bare],
]);

describe("view model with inline params", () => {
  it("threads document values into the node's inline params, pane-only kinds excluded", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "csv.read", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "x.csv" });
    const n = buildCanvasModel(store.getState(), catalog).nodes[0]!;
    expect(n.params.map((p) => p.name)).toEqual(["path", "header"]);
    expect(n.params[0]!.value).toBe("x.csv");
    expect(n.params[1]!.value).toBe("true"); // default applied
  });

  it("nodes with inline params are taller and wider; bare nodes keep the classic size", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "csv.read", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "bare.node", version: 1 });
    const model = buildCanvasModel(store.getState(), catalog);
    const [a, b] = [model.nodes[0]!, model.nodes[1]!];
    expect(a.w).toBe(WIDE_NODE_WIDTH);
    expect(b.w).toBe(NODE_WIDTH);
    expect(a.h).toBeGreaterThan(b.h);
    expect(a.h).toBe(nodeHeight(0, 1, a.params));
    expect(b.h).toBe(nodeHeight(0, 1));
  });
});

describe("freePosition", () => {
  const noOverlap = (
    a: { x: number; y: number },
    w: number,
    h: number,
    r: { x: number; y: number; w: number; h: number },
  ) => a.x + w <= r.x || r.x + r.w <= a.x || a.y + h <= r.y || r.y + r.h <= a.y;

  it("places the first node at the grid origin", () => {
    expect(freePosition([], 184, 76)).toEqual({ x: 80, y: 80 });
  });

  it("avoids a tall node instead of assuming a fixed row height", () => {
    const tall = { x: 80, y: 80, w: 240, h: 300 };
    const p = freePosition([tall], 240, 300);
    expect(noOverlap(p, 240, 300, tall)).toBe(true);
  });

  it("fills gaps between differently sized nodes without overlap", () => {
    const placed: { x: number; y: number; w: number; h: number }[] = [];
    for (const [w, h] of [[184, 76], [240, 320], [240, 180], [184, 56], [240, 260]] as const) {
      const p = freePosition(placed, w, h);
      for (const r of placed) expect(noOverlap(p, w, h, r)).toBe(true);
      placed.push({ ...p, w, h });
    }
  });
});
