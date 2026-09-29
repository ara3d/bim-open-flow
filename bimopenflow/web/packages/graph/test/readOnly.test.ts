// A read-only canvas (docs/plans/graph-editor-package.md, acceptance 3): it
// selects on click, and a wire drag, a node drag, Delete, a toggle press, and
// an island input all leave the document as it was. The gestures are driven
// through the runtime's public input pipeline, so both guards are exercised:
// the parts that decline to begin a mutation, and the update function that
// drops any mutating intent that still arrives.

import { describe, expect, it } from "vitest";
import { Runtime, rect, v, type Element, type GNode } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, MUTATING_INTENTS, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, NODE_HEADER, PORT_SPACING, type CanvasModel } from "../src/viewModel.js";
import { COMPACT_SLOT_H, SLOT_X_PAD, SLOTS_PAD_TOP } from "../src/canvasSlots.js";
import { portY } from "../src/portGeometry.js";
import { slotElement } from "../src/slotRegistry.js";
import { createCanvasInstance } from "../src/instance.js";

const desc: NodeDescriptor = {
  kind: "k.a",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [{ name: "header", kind: "Boolean", default: "true" }],
  description: "",
};
const catalog = new Map([["k.a", desc]]);

function mountViewer() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 100, y: 100 } });
  store.dispatch({ type: "addNode", id: "b", kind: "k.a", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "b", layout: { x: 600, y: 100 } });
  store.dispatch({ type: "markSaved" });
  const instance = createCanvasInstance({ document, readOnly: true });
  const raised: CanvasIntent["kind"][] = [];
  const errors: string[] = [];
  const update = makeCanvasUpdate(store, (m) => errors.push(m), undefined, {}, true);
  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    {
      init: buildCanvasModel(store.getState(), catalog),
      update: (doc, intent) => { raised.push(intent.kind); return update(doc, intent); },
      view: (doc) => canvasView(doc, instance),
    },
    { headless: true, width: 1000, height: 700 },
  );
  instance.dispatch = (intent) => runtime.dispatch(intent);
  runtime.step(3, 1 / 60);
  const before = store.getState().document;
  return { store, instance, runtime, raised, errors, before };
}

const drag = (runtime: Runtime<CanvasModel, CanvasIntent>, from: { x: number; y: number }, to: { x: number; y: number }) => {
  runtime.pointerDown(v(from.x, from.y));
  runtime.step(1, 1 / 60);
  runtime.pointerMove(v((from.x + to.x) / 2, (from.y + to.y) / 2));
  runtime.step(1, 1 / 60);
  runtime.pointerMove(v(to.x, to.y));
  runtime.step(1, 1 / 60);
  runtime.pointerUp(v(to.x, to.y));
  runtime.step(2, 1 / 60);
};

describe("read-only canvas", () => {
  it("a click selects, and a drag from a node's header never moves it", () => {
    const { store, runtime, raised, before, errors } = mountViewer();
    const a = runtime.doc.nodes.find((n) => n.id === "a")!;
    runtime.pointerDown(v(a.x + 40, a.y + 12));
    runtime.pointerUp(v(a.x + 40, a.y + 12));
    runtime.step(2, 1 / 60);
    expect(store.getState().selection).toEqual(["a"]);

    drag(runtime, { x: a.x + 40, y: a.y + 12 }, { x: a.x + 240, y: a.y + 180 });
    expect(store.getState().document).toBe(before);
    expect(store.getState().document.layout["a"]).toEqual({ x: 100, y: 100 });
    expect(raised).not.toContain("move");
    expect(raised).not.toContain("moveEnd");
    expect(errors).toEqual([]);
  });

  it("a drag from an output socket draws no wire and connects nothing", () => {
    const { store, runtime, raised, before } = mountViewer();
    const a = runtime.doc.nodes.find((n) => n.id === "a")!;
    const b = runtime.doc.nodes.find((n) => n.id === "b")!;
    drag(runtime, { x: a.x + a.w, y: portY(a.y, 0) }, { x: b.x, y: portY(b.y, 0) });
    expect(store.getState().document).toBe(before);
    expect(store.getState().document.structure.edges).toEqual([]);
    expect(raised).not.toContain("connect");
    expect(raised).not.toContain("wireDropped");
  });

  it("Delete with a node selected removes nothing", () => {
    const { store, runtime, before } = mountViewer();
    store.dispatch({ type: "select", ids: ["a"] });
    runtime.key("Delete", { shift: false, alt: false, ctrl: false });
    runtime.step(2, 1 / 60);
    expect(store.getState().document).toBe(before);
    expect(store.getState().document.structure.nodes.map((n) => n.id)).toEqual(["a", "b"]);
  });

  it("a toggle press changes no parameter", () => {
    const { store, runtime, before } = mountViewer();
    const a = runtime.doc.nodes.find((n) => n.id === "a")!;
    const toggleY = a.y + NODE_HEADER + PORT_SPACING + SLOTS_PAD_TOP + COMPACT_SLOT_H / 2;
    const toggleX = a.x + a.w - SLOT_X_PAD - 8;
    runtime.pointerDown(v(toggleX, toggleY));
    runtime.pointerUp(v(toggleX, toggleY));
    runtime.step(2, 1 / 60);
    expect(store.getState().document).toBe(before);
    expect(store.getState().document.values["a"]).toBeUndefined();
  });

  it("shows a viewer's hint instead of the editing gestures", () => {
    const store = createStore();
    const model = buildCanvasModel(store.getState(), catalog);
    const hintOf = (instance: ReturnType<typeof createCanvasInstance>): string => {
      const find = (e: Element): Element | undefined =>
        e.key === "hint" ? e : e.children?.map(find).find(Boolean);
      return (find(canvasView(model, instance))?.props as { text: string }).text;
    };
    expect(hintOf(createCanvasInstance({ document, readOnly: true }))).toBe("click = select · drag/wheel = pan/zoom");
    expect(hintOf(createCanvasInstance({ document }))).toContain("Del = cut");
  });

  it("island inputs are disabled, and the update drops every mutating intent", () => {
    const instance = createCanvasInstance({ document, readOnly: true });
    const element = slotElement({ nodeId: "a", param: { name: "name", kind: "Text", value: "x" }, w: 240, open: false, instance });
    const node: GNode<unknown> = { key: element.key, props: element.props, rect: rect(0, 0, 240, 32), ch: {}, states: new Set(), local: element.part.localInit };
    const el = (element.part.island!(node)?.el as HTMLInputElement | undefined);
    expect(el?.disabled).toBe(true);
    const column = instance.columnSelects.get("a", "sort", "", false);
    expect(column.querySelector("select")?.disabled).toBe(true);
    expect(column.querySelector("button")?.disabled).toBe(true);

    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    const update = makeCanvasUpdate(store, () => {}, undefined, {}, true);
    const doc = buildCanvasModel(store.getState(), catalog);
    const before = store.getState().document;
    for (const kind of MUTATING_INTENTS) {
      const intent = { kind, id: "a", nodeId: "a", name: "header", value: "false", x: 0, y: 0, a: "out:a.out", b: "in:b.in", from: "out:a.out" } as unknown as CanvasIntent;
      expect(update(doc, intent)).toBe(doc);
    }
    expect(store.getState().document).toBe(before);
  });
});
