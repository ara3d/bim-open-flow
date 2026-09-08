// Headless interaction tests for the inline node controls: presses on the
// canvas-drawn toggle and dropdown land in the store as setParam edits.

import { afterEach, describe, expect, it } from "vitest";
import { Runtime, rect, v, type Element, type GNode } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, NODE_HEADER, PORT_SPACING, type CanvasModel } from "../src/viewModel.js";
import { COMPACT_SLOT_H, SLOT_GAP, SLOT_X_PAD, SLOTS_PAD_TOP } from "../src/canvasSlots.js";
import { disposeInlineControls, setInlineControlDispatch, slotElement } from "../src/canvasControls.js";

afterEach(() => disposeInlineControls());

const slotNode = (element: Element): GNode<unknown> => ({
  key: element.key, props: element.props, rect: rect(0, 0, 240, 32),
  ch: {}, states: new Set(), local: element.part.localInit,
});
const inputFor = (element: Element): HTMLInputElement | null => {
  if (element.part.island) return element.part.island(slotNode(element))?.el as HTMLInputElement | undefined ?? null;
  return element.children?.map(inputFor).find(Boolean) ?? null;
};

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
  it("recreates a reused parameter input with refreshed bounds and percent conversion", () => {
    const intents: CanvasIntent[] = [];
    setInlineControlDispatch(intent => intents.push(intent));
    const original = inputFor(slotElement("same", {name:"value",kind:"Number",value:"0.5"},240))!;
    expect(original.value).toBe("0.5");
    const current = inputFor(slotElement("same", {
      name:"value",kind:"Fraction",value:"0.5",
      control:{kind:"slider",min:0,max:1,step:.01,unit:"percent"},
    },240))!;
    expect(current).not.toBe(original);
    expect([current.value,current.min,current.max,current.step]).toEqual(["50","0","100","1"]);
    current.value = "150";
    current.dispatchEvent(new Event("change"));
    expect(intents.at(-1)).toEqual({kind:"setParam",nodeId:"same",name:"value",value:"1"});
    expect(current.value).toBe("100");
    const text = inputFor(slotElement("same", {name:"value",kind:"Text",value:"hello"},240))!;
    expect(text.type).toBe("text");
    expect(text.min).toBe("");
    text.value = "world";
    text.dispatchEvent(new Event("change"));
    expect(intents.at(-1)).toMatchObject({value:"world"});
  });

  it("replacing an open enum under the same key restores native input islands", () => {
    const dropdown = slotElement("same", {name:"value",kind:"Enum",value:"a",enumValues:["a","b"]},240);
    dropdown.part.reduce!({open:false},{kind:"toggle"},slotNode(dropdown));
    const other = slotElement("other", {name:"value",kind:"Text",value:"visible"},240);
    expect(inputFor(other)).toBeNull();
    slotElement("same", {name:"value",kind:"Text",value:"replacement"},240);
    expect(inputFor(other)?.value).toBe("visible");
  });

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
