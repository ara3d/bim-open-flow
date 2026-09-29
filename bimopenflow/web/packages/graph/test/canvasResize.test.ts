import { describe, expect, it } from "vitest";
import { Runtime, v } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { RESIZE_GRAB_RADIUS, resizeHandleHit } from "../src/canvasResize.js";
import { MAX_NODE_SIZE, nodeSize } from "../src/nodeSize.js";
import { makeCanvasUpdate, MUTATING_INTENTS, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { createCanvasInstance } from "../src/instance.js";
import {
  buildCanvasModel,
  type CanvasModel,
  NODE_WIDTH,
  nodeHeight,
  NOTE_KIND,
  NOTE_WIDTH,
  noteHeight,
} from "../src/viewModel.js";

const card = { x: 100, y: 50, w: 200, h: 120 };

describe("resizeHandleHit", () => {
  it("takes a press on or just inside the bottom-right corner", () => {
    expect(resizeHandleHit(card, { x: 300, y: 170 })).toBe(true);
    expect(resizeHandleHit(card, { x: 294, y: 164 })).toBe(true);
  });

  it("leaves the rest of the card, and a socket centre 12 px up, to the other gestures", () => {
    expect(resizeHandleHit(card, { x: 300 - RESIZE_GRAB_RADIUS, y: 170 })).toBe(false);
    expect(resizeHandleHit(card, { x: 300, y: 158 })).toBe(false);
    expect(resizeHandleHit(card, { x: 100, y: 170 })).toBe(false);
  });

  it("reaches as far in screen pixels when zoomed out", () => {
    const hit = (screenPx: number, zoom: number) => resizeHandleHit(card, { x: 300 - screenPx / zoom, y: 170 }, zoom);
    expect(hit(8, 0.28)).toBe(true);
    expect(hit(12, 0.28)).toBe(false);
    expect(hit(8, 1)).toBe(true);
    // Zoomed in, the reach stays 10 world units: 16 pixels at zoom 2.
    expect(hit(16, 2)).toBe(true);
    expect(hit(24, 2)).toBe(false);
  });

  it("gives a press nearer a socket than the corner to the socket", () => {
    const socket = { x: 300, y: 158 };
    expect(resizeHandleHit(card, { x: 299, y: 162 }, 0.28, [socket])).toBe(false);
    expect(resizeHandleHit(card, { x: 297, y: 168 }, 0.28, [socket])).toBe(true);
  });
});

describe("nodeSize for a resize", () => {
  const plain = { id: "a", kind: "k", inputs: [], outputs: [], params: [] };
  const note = { id: "n", kind: NOTE_KIND, inputs: [], outputs: [], params: [], noteText: "hello" };

  it("holds a dragged width between the least card width and MAX_NODE_SIZE", () => {
    expect(nodeSize(plain, { w: 10 }).w).toBe(NODE_WIDTH);
    expect(nodeSize(plain, { w: 400 }).w).toBe(400);
    expect(nodeSize(plain, { w: 5000 }).w).toBe(MAX_NODE_SIZE);
  });

  it("keeps a plain card's height to its content", () => {
    expect(nodeSize(plain, { w: 300, h: 999 }).h).toBe(nodeHeight(0, 0));
  });

  it("lets a note grow taller but not shorter than its text", () => {
    expect(nodeSize(note, { w: 10, h: 10 })).toEqual({ w: NOTE_WIDTH, h: noteHeight("hello") });
    expect(nodeSize(note, { w: 500, h: 600 })).toEqual({ w: 500, h: 600 });
  });
});

const noteDesc: NodeDescriptor = {
  kind: NOTE_KIND,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [],
  params: [{ name: "text", kind: "Text", default: "" }],
  description: "",
};
const catalog = new Map([[NOTE_KIND, noteDesc]]);

function setup(readOnly = false) {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
  store.dispatch({ type: "addNode", id: "n", kind: NOTE_KIND, version: 1 });
  const update = makeCanvasUpdate(store, () => {}, undefined, {}, readOnly);
  const model = () => buildCanvasModel(store.getState(), catalog);
  return { store, update, model };
}

describe("resize intents", () => {
  it("keeps the size transient until resizeEnd, then saves width with the position", () => {
    const { store, update, model } = setup();
    let doc = update(model(), { kind: "resize", id: "a", w: 420, h: 999 });
    expect(doc.nodes.find((n) => n.id === "a")?.w).toBe(420);
    expect(store.getState().document.layout["a"]).toBeUndefined();
    update(doc, { kind: "resizeEnd", id: "a" });
    const { x, y } = doc.nodes.find((n) => n.id === "a")!;
    // A plain card's height follows its content, so only w is saved.
    expect(store.getState().document.layout["a"]).toEqual({ x, y, w: 420 });
    expect(model().nodes.find((n) => n.id === "a")?.w).toBe(420);
  });

  it("saves a note's height too", () => {
    const { store, update, model } = setup();
    const doc = update(model(), { kind: "resize", id: "n", w: 360, h: 240 });
    update(doc, { kind: "resizeEnd", id: "n" });
    expect(store.getState().document.layout["n"]).toMatchObject({ w: 360, h: 240 });
    expect(model().nodes.find((n) => n.id === "n")).toMatchObject({ w: 360, h: 240 });
  });

  it("keeps the saved size when a later move writes the position (setLayout merge)", () => {
    const { store, update, model } = setup();
    update(update(model(), { kind: "resize", id: "a", w: 420, h: 0 }), { kind: "resizeEnd", id: "a" });
    update(update(model(), { kind: "move", id: "a", x: 700, y: 30 }), { kind: "moveEnd", id: "a" });
    expect(store.getState().document.layout["a"]).toEqual({ x: 700, y: 30, w: 420 });
  });

  it("is one undo step", () => {
    const { store, update, model } = setup();
    update(update(model(), { kind: "resize", id: "a", w: 420, h: 0 }), { kind: "resizeEnd", id: "a" });
    store.dispatch({ type: "undo" });
    expect(store.getState().document.layout["a"]).toBeUndefined();
  });

  it("is dropped on a read-only canvas", () => {
    expect(MUTATING_INTENTS.has("resize")).toBe(true);
    expect(MUTATING_INTENTS.has("resizeEnd")).toBe(true);
    const { store, update, model } = setup(true);
    const doc = model();
    expect(update(doc, { kind: "resize", id: "a", w: 420, h: 0 })).toBe(doc);
    update(doc, { kind: "resizeEnd", id: "a" });
    expect(store.getState().document.layout["a"]).toBeUndefined();
  });
});

// TKT-125: the gesture end to end, driven through gratify's runtime the way
// the browser drives it (pointerDown, pointerMove, pointerUp in screen
// pixels), with the store re-synced into the canvas after every change as
// canvasEditor does.
describe("corner drag through the runtime", () => {
  const tableDesc: NodeDescriptor = {
    kind: "k.t", version: 1, capability: "Pure", params: [], description: "",
    inputs: [{ name: "in", type: "Table", optional: false }],
    outputs: [{ name: "out", type: "Table", optional: false }],
  };
  const catalog = new Map([["k.t", tableDesc]]);
  const instance = createCanvasInstance({ document });

  function mount(zoom: number) {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.t", version: 1 });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 100, y: 100 } });
    const model = () => buildCanvasModel(store.getState(), catalog);
    const runtime = new Runtime<CanvasModel, CanvasIntent>(
      null,
      { init: model(), update: makeCanvasUpdate(store, () => {}), view: (doc) => canvasView(doc, instance) },
      { headless: true, width: 1200, height: 800 },
    );
    store.subscribe(() => runtime.dispatch({ kind: "sync", model: model() }));
    runtime.viewport = { zoom, pan: v(0, 0) };
    runtime.step(3, 1 / 60);
    const screen = (x: number, y: number) => v(x * zoom, y * zoom);
    /** Presses at `from` (world), moves in steps to `from` + `by` screen
     *  pixels, releases, and lets the canvas settle. */
    const drag = (from: { x: number; y: number }, by: { x: number; y: number }) => {
      const p0 = screen(from.x, from.y);
      runtime.pointerDown(p0);
      runtime.step(1, 1 / 60);
      for (let i = 1; i <= 5; i++) {
        runtime.pointerMove(v(p0.x + (by.x * i) / 5, p0.y + (by.y * i) / 5));
        runtime.step(1, 1 / 60);
      }
      runtime.pointerUp(v(p0.x + by.x, p0.y + by.y));
      runtime.step(3, 1 / 60);
    };
    return { store, runtime, drag, card: () => runtime.doc.nodes[0]! };
  }

  it("adds exactly one undo step, and one undo restores the width", () => {
    const { store, drag, card } = mount(1);
    const { x, y, w, h } = card();
    const steps = store.getState().undoStack.length;
    drag({ x: x + w - 2, y: y + h - 2 }, { x: 80, y: 0 });
    expect(store.getState().undoStack.length).toBe(steps + 1);
    expect(store.getState().document.layout["a"]).toEqual({ x, y, w: w + 80 });
    expect(card().w).toBe(w + 80);
    store.dispatch({ type: "undo" });
    expect(store.getState().document.layout["a"]).toEqual({ x, y });
    expect(card().w).toBe(w);
  });

  it("grabs the corner at a fitted zoom, where 10 world units are under 3 pixels", () => {
    const zoom = 0.28;
    const { store, drag, card } = mount(zoom);
    const { x, y, w, h } = card();
    // 3 screen pixels left of the corner and 1 above: 11 world units away.
    drag({ x: x + w - 3 / zoom, y: y + h - 1 / zoom }, { x: 20, y: 0 });
    expect(store.getState().document.layout["a"]!.w).toBeCloseTo(w + 20 / zoom);
  });

  it("still starts a wire from the last output socket at that zoom", () => {
    const zoom = 0.28;
    const { store, drag, card } = mount(zoom);
    const { x, y, w } = card();
    // On the output socket, 12 world units above the corner.
    drag({ x: x + w, y: y + 46 + 12 }, { x: 60, y: 0 });
    expect(store.getState().document.layout["a"]).toEqual({ x, y });
  });
});
