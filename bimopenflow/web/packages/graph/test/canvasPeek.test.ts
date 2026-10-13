// C7: canvasParts.ts draws a wire's row count and the peek card at its port.
// Headless-gratify smoke, the same pattern as canvasParts.test.ts and
// canvasFlowMotion.test.ts.

import { describe, expect, it } from "vitest";
import { Runtime, type Color, type Painter, type Vec } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView, PARAM_WIRE, RELATION_STROKE } from "../src/canvasParts.js";
import { createCanvasInstance } from "../src/instance.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";
import { NO_PORT_RESULTS, type PortResultsView } from "../src/portResults.js";

const instance = createCanvasInstance({ document });

const desc: NodeDescriptor = {
  kind: "k.a",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

/** Records every label() call's text so a test can tell whether the row-count
 *  label or the peek card drew anything, without a real canvas. */
class SpyPainter implements Painter {
  labels: string[] = [];
  /** Stroke widths of every wire() call, in draw order. */
  wires: number[] = [];
  measure = { text: () => ({ x: 0, y: 0 }) };
  clear() {}
  box() {}
  label(text: string) { this.labels.push(text); }
  dot() {}
  ring() {}
  arc() {}
  poly() {}
  line() {}
  glow(_c: Color, _b: number, draw: () => void) { draw(); }
  push() {}
  pop() {}
  alpha() {}
  translate() {}
  scaleAt() {}
  screen() {}
  view() {}
  wire(_a: Vec, _b: Vec, _c: Color, lw: number) { this.wires.push(lw); }
  clip() {}
}

function mountDiamond(results: PortResultsView, outType: "Table" | "Relation" = "Table"): { runtime: Runtime<CanvasModel, CanvasIntent>; spy: SpyPainter } {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: "k.a", version: 1 });
  store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
  const catalog = new Map([["k.a", { ...desc, outputs: [{ name: "out", type: outType, optional: false }], inputs: [{ name: "in", type: outType, optional: false }] }]]);
  const model = buildCanvasModel(store.getState(), catalog, null, results);

  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    { init: model, update: makeCanvasUpdate(store, () => {}), view: (doc) => canvasView(doc, instance) },
    { headless: true, width: 800, height: 600 },
  );
  const spy = new SpyPainter();
  runtime.painter = spy;
  return { runtime, spy };
}

describe("canvas parts draw a parameter wire thinner", () => {
  it("strokes a wire into a parameter socket narrower than a wire into a port", () => {
    const port = mountDiamond(NO_PORT_RESULTS);
    port.runtime.step(3, 1 / 60);
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "k.p", version: 1 });
    store.dispatch({ type: "connect", from: "a.out", to: "b.path" });
    const withParam: NodeDescriptor = { ...desc, kind: "k.p", inputs: [], params: [{ name: "path", kind: "FilePath", default: "", bindable: "Table" }] };
    const model = buildCanvasModel(store.getState(), new Map([["k.a", desc], ["k.p", withParam]]), null, NO_PORT_RESULTS);
    expect(model.edges[0]!.param).toBe(true);
    const runtime = new Runtime<CanvasModel, CanvasIntent>(null,
      { init: model, update: makeCanvasUpdate(store, () => {}), view: (doc) => canvasView(doc, instance) },
      { headless: true, width: 800, height: 600 });
    const spy = new SpyPainter();
    runtime.painter = spy;
    runtime.step(3, 1 / 60);
    expect(spy.wires[1]!).toBeCloseTo(port.spy.wires[1]! * PARAM_WIRE.width);
  });
});

describe("canvas parts draw a Relation wire hollow", () => {
  it("strokes a Table wire twice (shadow, line) and a Relation wire three times, the last in the background colour", () => {
    const table = mountDiamond(NO_PORT_RESULTS);
    table.runtime.step(3, 1 / 60);
    const relation = mountDiamond(NO_PORT_RESULTS, "Relation");
    relation.runtime.step(3, 1 / 60);
    // Three frames each; every frame strokes the one wire the same way.
    expect(table.spy.wires).toHaveLength(3 * 2);
    expect(relation.spy.wires).toHaveLength(3 * 3);
    const [, tableLine] = table.spy.wires;
    const [, relationOuter, relationInner] = relation.spy.wires;
    expect(relationOuter!).toBeGreaterThan(tableLine!);
    expect(relationInner).toBe(RELATION_STROKE.inner);
  });
});

describe("canvas parts draw wire row counts and the peek card (TKT-11 C7)", () => {
  it("steps without errors and paints the row-count label when rows and peek are set", () => {
    const errors: string[] = [];
    const results: PortResultsView = {
      counts: new Map([["a.out", { rows: 2, current: true }]]),
      peek: { endpoint: "a.out", pinned: false, peek: { kind: "ready", slice: { columns: [], rows: [], totalRows: 2, skip: 0 } } },
    };
    const { runtime, spy } = mountDiamond(results);
    try {
      runtime.step(3, 1 / 60);
    } catch (err) {
      errors.push(String(err));
    }
    expect(errors).toEqual([]);
    expect(spy.labels).toContain("2 rows");
  });

  it("paints no row label for a wire without rows", () => {
    const { runtime, spy } = mountDiamond({ counts: new Map(), peek: null });
    runtime.step(3, 1 / 60);
    expect(spy.labels).not.toContain("2 rows");
    expect(spy.labels.some((t) => /rows|row$/.test(t))).toBe(false);
  });
});
