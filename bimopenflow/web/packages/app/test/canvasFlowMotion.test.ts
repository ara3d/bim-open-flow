// TKT-24: the wire-flow animation paints a travelling highlight while motion
// is allowed, and falls back to a static highlight under
// prefers-reduced-motion — the same fallback selectionBorder.ts uses.

import { afterEach, describe, expect, it } from "vitest";
import { Runtime, type Color, type Painter } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";

const desc: NodeDescriptor = {
  kind: "k.a",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

/** Records every dot() radius and every wire() call so a test can tell a
 *  travelling highlight (small dots at an animated position) apart from a
 *  static one (an extra wire stroke, no dots beyond the port sockets). */
class SpyPainter implements Painter {
  dotRadii: number[] = [];
  wireCalls: { color: Color; lw: number }[] = [];
  measure = { text: () => ({ x: 0, y: 0 }) };
  clear() {}
  box() {}
  label() {}
  dot(_p: unknown, r: number) { this.dotRadii.push(r); }
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
  wire(_a: unknown, _b: unknown, color: Color, lw: number) { this.wireCalls.push({ color, lw }); }
  clip() {}
}

function mountDiamond(): { runtime: Runtime<CanvasModel, CanvasIntent>; spy: SpyPainter } {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: "k.a", version: 1 });
  store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
  store.dispatch({ type: "select", ids: ["b"] });
  const catalog = new Map([["k.a", desc]]);
  const model = buildCanvasModel(store.getState(), catalog);

  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    { init: model, update: makeCanvasUpdate(store, () => {}), view: canvasView },
    { headless: true, width: 800, height: 600 },
  );
  const spy = new SpyPainter();
  runtime.painter = spy;
  return { runtime, spy };
}

const FLOW_DOT_RADIUS = 3.4;

describe("wire-flow motion", () => {
  const originalMatchMedia = window.matchMedia;
  afterEach(() => {
    window.matchMedia = originalMatchMedia;
  });

  it("paints a travelling dot along the flowing wire when motion is allowed", () => {
    window.matchMedia = ((query: string) => ({ matches: false, media: query }) as MediaQueryList) as typeof window.matchMedia;
    const { runtime, spy } = mountDiamond();
    runtime.step(3, 1 / 60);
    expect(spy.dotRadii.filter((r) => Math.abs(r - FLOW_DOT_RADIUS) < 1e-6).length).toBeGreaterThan(0);
  });

  it("falls back to a static wire highlight under prefers-reduced-motion", () => {
    window.matchMedia = ((query: string) => ({ matches: true, media: query }) as MediaQueryList) as typeof window.matchMedia;
    const { runtime, spy } = mountDiamond();
    runtime.step(3, 1 / 60);
    expect(spy.dotRadii.some((r) => Math.abs(r - FLOW_DOT_RADIUS) < 1e-6)).toBe(false);
    // Shadow (4) + base (2..) + static flow highlight (3) wire strokes.
    expect(spy.wireCalls.some((c) => Math.abs(c.lw - 3) < 1e-6)).toBe(true);
  });
});
