// Two canvases on one page keep separate state (docs/plans/graph-editor-
// package.md, acceptance 2): two headless runtimes over two stores whose
// graphs reuse the node id "n1". A toggle press in the first reaches only the
// first store; the same parameter row gets one island input per instance; and
// pruning the first instance leaves the second's rows in place.

import { describe, expect, it } from "vitest";
import { Runtime, rect, v, type Element, type GNode } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore, type Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, NODE_HEADER, PORT_SPACING, type CanvasModel } from "../src/viewModel.js";
import { COMPACT_SLOT_H, SLOT_X_PAD, SLOTS_PAD_TOP } from "../src/canvasSlots.js";
import { slotElement } from "../src/slotRegistry.js";
import { createCanvasInstance, pruneInstance, type CanvasInstance } from "../src/instance.js";

const desc: NodeDescriptor = {
  kind: "csv.like",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "header", kind: "Boolean", default: "true" },
    { name: "name", kind: "Text", default: "" },
  ],
  description: "",
};
const catalog = new Map([["csv.like", desc]]);

interface Mounted {
  store: Store;
  instance: CanvasInstance;
  runtime: Runtime<CanvasModel, CanvasIntent>;
  intents: CanvasIntent[];
}

/** One store, one instance, one headless runtime, all with node "n1" at (100, 100). */
function mountOne(): Mounted {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "n1", kind: "csv.like", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "n1", layout: { x: 100, y: 100 } });
  const instance = createCanvasInstance({ document });
  const intents: CanvasIntent[] = [];
  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    {
      init: buildCanvasModel(store.getState(), catalog),
      update: makeCanvasUpdate(store, (m) => { throw new Error(m); }),
      view: (doc) => canvasView(doc, instance),
    },
    { headless: true, width: 900, height: 700 },
  );
  instance.dispatch = (intent) => { intents.push(intent); runtime.dispatch(intent); };
  runtime.step(3, 1 / 60);
  return { store, instance, runtime, intents };
}

const slotNode = (element: Element): GNode<unknown> => ({
  key: element.key, props: element.props, rect: rect(0, 0, 240, 32),
  ch: {}, states: new Set(), local: element.part.localInit,
});
const inputFor = (element: Element): HTMLInputElement | null => {
  if (element.part.island) return element.part.island(slotNode(element))?.el as HTMLInputElement | undefined ?? null;
  return element.children?.map(inputFor).find(Boolean) ?? null;
};

const header = (m: Mounted) => m.store.getState().document.values["n1"]?.["header"];

describe("two canvases on one page", () => {
  it("a toggle press in one canvas reaches only its own store", () => {
    const a = mountOne();
    const b = mountOne();
    const n = a.runtime.doc.nodes[0]!;
    const toggleY = 100 + NODE_HEADER + PORT_SPACING + SLOTS_PAD_TOP + COMPACT_SLOT_H / 2;
    const toggleX = 100 + n.w - SLOT_X_PAD - 8;
    a.runtime.pointerDown(v(toggleX, toggleY));
    a.runtime.pointerUp(v(toggleX, toggleY));
    a.runtime.step(2, 1 / 60);
    expect(header(a)).toBe("false");
    expect(header(b)).toBeUndefined();
  });

  it("the same parameter row gets one island input per instance, committing to its own dispatch", () => {
    const a = mountOne();
    const b = mountOne();
    const ctx = (instance: CanvasInstance) =>
      ({ nodeId: "n1", param: { name: "name", kind: "Text" as const, value: "" }, w: 240, open: false, instance });
    const inputA = inputFor(slotElement(ctx(a.instance)))!;
    const inputB = inputFor(slotElement(ctx(b.instance)))!;
    expect(inputA).not.toBe(inputB);
    expect(a.instance.islands.get("n1::name")?.el).toBe(inputA);
    expect(b.instance.islands.get("n1::name")?.el).toBe(inputB);

    inputA.value = "doors";
    inputA.dispatchEvent(new Event("change"));
    expect(a.intents).toEqual([{ kind: "setParam", nodeId: "n1", name: "name", value: "doors" }]);
    expect(b.intents).toEqual([]);
    expect(a.store.getState().document.values["n1"]?.["name"]).toBe("doors");
    expect(b.store.getState().document.values["n1"]?.["name"]).toBeUndefined();
  });

  it("pruning one instance leaves the other's rows in place", () => {
    const a = mountOne();
    const b = mountOne();
    const ctx = (instance: CanvasInstance) =>
      ({ nodeId: "n1", param: { name: "name", kind: "Text" as const, value: "" }, w: 240, open: false, instance });
    inputFor(slotElement(ctx(a.instance)));
    inputFor(slotElement(ctx(b.instance)));
    pruneInstance(a.instance, new Set());
    expect(a.instance.islands.size).toBe(0);
    expect(b.instance.islands.size).toBe(1);
    expect(inputFor(slotElement(ctx(b.instance)))).toBe(b.instance.islands.get("n1::name")?.el);
  });
});
