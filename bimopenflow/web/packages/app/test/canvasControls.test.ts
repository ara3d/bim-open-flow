// Headless interaction tests for the inline node controls: presses on the
// canvas-drawn toggle and dropdown land in the store as setParam edits.

import { describe, expect, it } from "vitest";
import { Runtime, v } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, NODE_HEADER, PORT_SPACING, type CanvasModel } from "../src/viewModel.js";
import { COMPACT_SLOT_H, SLOT_GAP, SLOT_X_PAD, SLOTS_PAD_TOP } from "../src/canvasSlots.js";

const desc: NodeDescriptor = {
  kind: "csv.like",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "header", kind: "Boolean", default: "true" },
    { name: "mode", kind: "Enum", default: "left", enumValues: ["left", "inner", "anti"] },
  ],
  description: "",
};

const setup = () => {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "n1", kind: "csv.like", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "n1", layout: { x: 100, y: 100 } });
  const catalog = new Map([["csv.like", desc]]);
  const model = () => buildCanvasModel(store.getState(), catalog);
  const errors: string[] = [];
  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    { init: model(), update: makeCanvasUpdate(store, (m) => errors.push(m)), view: canvasView },
    { headless: true, width: 900, height: 700 },
  );
  runtime.step(3, 1 / 60);
  const sync = () => {
    runtime.dispatch({ kind: "sync", model: model() });
    runtime.step(3, 1 / 60);
  };
  return { store, runtime, errors, sync };
};

const click = (runtime: Runtime<CanvasModel, CanvasIntent>, x: number, y: number) => {
  runtime.pointerDown(v(x, y));
  runtime.pointerUp(v(x, y));
  runtime.step(2, 1 / 60);
};

// Node at (100,100), one port row: slots start below header + 1 port row.
const slotsTop = 100 + NODE_HEADER + PORT_SPACING + SLOTS_PAD_TOP;
const node = () => ({ x: 100, y: 100 });

describe("inline node controls (headless)", () => {
  it("clicking the toggle flips the Boolean param in the store", () => {
    const { store, runtime, errors } = setup();
    const model = runtime.doc;
    const n = model.nodes[0]!;
    const toggleY = slotsTop + COMPACT_SLOT_H / 2;
    const toggleX = node().x + n.w - SLOT_X_PAD - 8; // inside the switch
    click(runtime, toggleX, toggleY);
    expect(errors).toEqual([]);
    expect(store.getState().document.values["n1"]?.["header"]).toBe("false");
  });

  it("clicking the toggle does not start a node drag or move the node", () => {
    const { store, runtime } = setup();
    const n = runtime.doc.nodes[0]!;
    const toggleY = slotsTop + COMPACT_SLOT_H / 2;
    click(runtime, node().x + n.w - SLOT_X_PAD - 8, toggleY);
    expect(store.getState().document.layout["n1"]).toMatchObject({ x: 100, y: 100 });
  });

  it("opening the dropdown and picking an option sets the Enum param", () => {
    const { store, runtime, errors, sync } = setup();
    const n = runtime.doc.nodes[0]!;
    const enumY = slotsTop + COMPACT_SLOT_H + SLOT_GAP + COMPACT_SLOT_H / 2;
    const fieldX = node().x + n.w - SLOT_X_PAD - 20; // inside the value field
    click(runtime, fieldX, enumY); // open
    // First option row sits just below the field, inside the modal panel.
    const optionX = node().x + n.w / 2;
    const optionY = enumY + COMPACT_SLOT_H / 2 + 3 + 5 + 12 + 1;
    click(runtime, optionX, optionY);
    sync();
    expect(errors).toEqual([]);
    expect(store.getState().document.values["n1"]?.["mode"]).toBe("left");
  });

  it("param edits from the canvas participate in undo", () => {
    const { store, runtime } = setup();
    const n = runtime.doc.nodes[0]!;
    click(runtime, node().x + n.w - SLOT_X_PAD - 8, slotsTop + COMPACT_SLOT_H / 2);
    expect(store.getState().document.values["n1"]?.["header"]).toBe("false");
    store.dispatch({ type: "undo" });
    expect(store.getState().document.values["n1"]?.["header"]).toBeUndefined();
  });
});
