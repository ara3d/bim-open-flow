import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { clampNodeSize, MAX_NODE_SIZE, RESIZE_GRAB_RADIUS, resizeHandleHit } from "../src/canvasResize.js";
import { makeCanvasUpdate, MUTATING_INTENTS } from "../src/canvasIntents.js";
import {
  buildCanvasModel,
  fitNodeSize,
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
});

describe("clampNodeSize", () => {
  const content = { w: 184, h: 70 };

  it("holds width between the content width and MAX_NODE_SIZE", () => {
    expect(clampNodeSize(content, { w: 100 }, false).w).toBe(184);
    expect(clampNodeSize(content, { w: 400 }, false).w).toBe(400);
    expect(clampNodeSize(content, { w: 5000 }, false).w).toBe(MAX_NODE_SIZE);
  });

  it("falls back to the content size when nothing is asked for", () => {
    expect(clampNodeSize(content, {}, true)).toEqual(content);
  });

  it("resizes height only when asked to", () => {
    expect(clampNodeSize(content, { w: 300, h: 400 }, false).h).toBe(70);
    expect(clampNodeSize(content, { w: 300, h: 400 }, true).h).toBe(400);
    expect(clampNodeSize(content, { w: 300, h: 10 }, true).h).toBe(70);
  });

  it("never shrinks content wider than the maximum", () => {
    expect(clampNodeSize({ w: 1000, h: 70 }, { w: 200 }, false).w).toBe(1000);
  });
});

describe("fitNodeSize", () => {
  it("uses the default card size as the minimum", () => {
    const plain = { kind: "k", inputs: [], outputs: [], params: [] };
    expect(fitNodeSize(plain, { w: 10, h: 999 })).toEqual({ w: NODE_WIDTH, h: nodeHeight(0, 0) });
  });

  it("lets a note grow taller but not shorter than its text", () => {
    const note = { kind: NOTE_KIND, inputs: [], outputs: [], params: [], noteText: "hello" };
    expect(fitNodeSize(note, { w: 10, h: 10 })).toEqual({ w: NOTE_WIDTH, h: noteHeight("hello") });
    expect(fitNodeSize(note, { w: 500, h: 600 })).toEqual({ w: 500, h: 600 });
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
