// Headless coverage for the kinds that used to live only in the properties
// pane — Json, Expression, and ModelRef — plus a Text parameter with a
// ColumnsOf suggestion and a Fraction parameter with a range control. This
// replaces the pane's test: every kind is editable from slotElement now, so
// the pane (deleted in C8) has no caller left. Each case
// drives the real factory the way a user would (an island's DOM events, or
// the long-text row's press-then-type-then-commit), feeds the resulting
// intent(s) through makeCanvasUpdate into a real store, and checks the
// document value and a single undo step, the way test/canvasControls.test.ts
// and test/canvasLongSlot.test.ts already do for the simpler kinds.

import { afterEach, describe, expect, it } from "vitest";
import { rect, type Element, type GNode } from "gratify";
import type { NodeDescriptor, ParamKind } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";
import { KIND_CONTROL, slotControl, type SlotContext, type CanvasParam } from "../src/canvasSlots.js";
import { disposeSlots, slotElement } from "../src/slotRegistry.js";
import { setInlineControlDispatch } from "../src/slotShared.js";

afterEach(() => {
  // disposeSlots (fixed for defect 3) now disposes the long-value editors
  // too, so this no longer needs a separate pruneLongValueEditors call.
  disposeSlots();
  setInlineControlDispatch(() => {});
});

/** One node whose params cover every kind this chunk moves onto the card. */
const desc: NodeDescriptor = {
  kind: "check.rule",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [],
  params: [
    { name: "rules", kind: "Json", default: "{}" },
    { name: "expr", kind: "Expression", default: "area > 10" },
    { name: "model", kind: "ModelRef", default: "" },
    { name: "cols", kind: "Text", default: "a,b", suggest: { kind: "ColumnsOfInput", source: "table" } },
    { name: "band", kind: "Fraction", default: "0.5", control: { kind: "range", min: 0, max: 1, step: 0.01 } },
  ],
  description: "",
};
const catalog = new Map([["check.rule", desc]]);

function setup() {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "n1", kind: "check.rule", version: 1 });
  const errors: string[] = [];
  const update = makeCanvasUpdate(store, (m) => errors.push(m));
  const model = () => buildCanvasModel(store.getState(), catalog);
  return { store, update, errors, model };
}

/** The SlotContext for one parameter of node "n1" in a given document. */
function paramCtx(doc: CanvasModel, name: string): SlotContext {
  const node = doc.nodes.find((n) => n.id === "n1")!;
  const param = node.params.find((p) => p.name === name)!;
  return {
    nodeId: "n1",
    param,
    w: 240,
    open: doc.openEditor?.nodeId === "n1" && doc.openEditor?.name === name,
  };
}

/** A minimal GNode wrapper, generic over the factory's own props type, the
 *  same shape test/canvasControls.test.ts uses to reach island() and on[]. */
const slotNode = (element: Element): GNode<unknown> => ({
  key: element.key,
  props: element.props,
  rect: rect(0, 0, 240, 200),
  ch: {},
  states: new Set(),
  local: element.part.localInit,
});

const pressIntent = (element: Element): CanvasIntent => {
  const press = element.part.on!.find((i) => i.kind === "press") as {
    kind: "press";
    to(node: GNode<unknown>): CanvasIntent;
  };
  return press.to(slotNode(element));
};

const islandOf = (element: Element): HTMLElement =>
  element.part.island!(slotNode(element))!.el as HTMLElement;

describe("Json and Expression: the long-text row on the node card", () => {
  it("opening the row, editing, and Ctrl+Enter commits one setParam; one undo reverts it", () => {
    const { store, update, model } = setup();
    let doc = model();

    const closed = slotElement(paramCtx(doc, "rules"));
    expect(slotControl(paramCtx(doc, "rules").param)).toBe("longText");
    doc = update(doc, pressIntent(closed));
    expect(doc.openEditor).toEqual({ nodeId: "n1", name: "rules" });

    const open = slotElement(paramCtx(doc, "rules"));
    const textarea = islandOf(open).querySelector("textarea") as HTMLTextAreaElement;
    expect(textarea.value).toBe("{}");

    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((i) => intents.push(i));
    textarea.value = '{"a":2}';
    textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }),
    );
    expect(intents).toEqual([
      { kind: "setParam", nodeId: "n1", name: "rules", value: '{"a":2}' },
      { kind: "closeEditor" },
    ]);

    doc = intents.reduce((d, i) => update(d, i), doc);
    expect(store.getState().document.values["n1"]?.["rules"]).toBe('{"a":2}');
    expect(doc.openEditor).toBeNull();

    store.dispatch({ type: "undo" });
    expect(store.getState().document.values["n1"]?.["rules"]).toBeUndefined();
  });

  it("committing unchanged text dispatches nothing, only closeEditor", () => {
    const { update, model } = setup();
    let doc = model();

    const closed = slotElement(paramCtx(doc, "expr"));
    doc = update(doc, pressIntent(closed));

    const open = slotElement(paramCtx(doc, "expr"));
    const textarea = islandOf(open).querySelector("textarea") as HTMLTextAreaElement;
    expect(textarea.value).toBe("area > 10"); // unchanged

    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((i) => intents.push(i));
    textarea.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }),
    );
    expect(intents).toEqual([{ kind: "closeEditor" }]);
  });
});

describe("ModelRef: a plain text field on the node card", () => {
  it("editing the island commits one setParam; one undo reverts it", () => {
    const { store, update, model } = setup();
    let doc = model();

    const ctx = paramCtx(doc, "model");
    expect(slotControl(ctx.param)).toBe("field");
    const element = slotElement(ctx);
    const input = islandOf(element) as HTMLInputElement;
    expect(input.type).toBe("text");

    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((i) => intents.push(i));
    input.value = "duplex.bos";
    input.dispatchEvent(new Event("change"));
    expect(intents).toEqual([{ kind: "setParam", nodeId: "n1", name: "model", value: "duplex.bos" }]);

    doc = intents.reduce((d, i) => update(d, i), doc);
    expect(store.getState().document.values["n1"]?.["model"]).toBe("duplex.bos");

    store.dispatch({ type: "undo" });
    expect(store.getState().document.values["n1"]?.["model"]).toBeUndefined();
  });
});

describe("Text with a ColumnsOf suggestion: keeps its field", () => {
  it("the suggestion source keeps the row a plain field, and commits through it", () => {
    const { store, update, model } = setup();
    let doc = model();

    const ctx = paramCtx(doc, "cols");
    expect(slotControl(ctx.param)).toBe("field"); // suggest overrides the long-text rule
    const element = slotElement(ctx);
    const input = islandOf(element) as HTMLInputElement;

    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((i) => intents.push(i));
    input.value = "c,d";
    input.dispatchEvent(new Event("change"));
    expect(intents).toEqual([{ kind: "setParam", nodeId: "n1", name: "cols", value: "c,d" }]);

    doc = intents.reduce((d, i) => update(d, i), doc);
    expect(store.getState().document.values["n1"]?.["cols"]).toBe("c,d");

    store.dispatch({ type: "undo" });
    expect(store.getState().document.values["n1"]?.["cols"]).toBeUndefined();
  });
});

describe("a range control", () => {
  it("rangeSlot's set callback commits one setParam; one undo reverts it", () => {
    const { store, update, model } = setup();
    let doc = model();

    const ctx = paramCtx(doc, "band");
    expect(slotControl(ctx.param)).toBe("range");
    const widget = slotElement(ctx);
    const rangeEl = widget.children!.find((c) => c.key === "range")!;
    const set = (rangeEl.props as { set(min: number, max: number): CanvasIntent }).set;
    const intent = set(0.3, 0.6);
    expect(intent).toEqual({ kind: "setParam", nodeId: "n1", name: "band", value: JSON.stringify([0.3, 0.6]) });

    doc = update(doc, intent);
    expect(store.getState().document.values["n1"]?.["band"]).toBe(JSON.stringify([0.3, 0.6]));

    store.dispatch({ type: "undo" });
    expect(store.getState().document.values["n1"]?.["band"]).toBeUndefined();
  });
});

describe("disposeSlots", () => {
  it("also disposes the long-value editors, not just the inline islands", () => {
    // Regression for defect 3: disposeSlots used to call only
    // disposeInlineControls, leaving longTextSlot's editors (and their
    // textarea and listeners) alive across a full canvas teardown.
    const { update, model } = setup();
    let doc = model();

    const closed = slotElement(paramCtx(doc, "rules"));
    doc = update(doc, pressIntent(closed));
    const firstOpen = slotElement(paramCtx(doc, "rules"));
    const firstTextarea = islandOf(firstOpen).querySelector("textarea") as HTMLTextAreaElement;

    disposeSlots();

    const secondOpen = slotElement(paramCtx(doc, "rules"));
    const secondTextarea = islandOf(secondOpen).querySelector("textarea") as HTMLTextAreaElement;

    expect(secondTextarea).not.toBe(firstTextarea);
  });
});

describe("Unknown kinds", () => {
  it("an unknown ParamKind falls back to field through slotElement", () => {
    const { model } = setup();
    const doc = model();

    const unknownParam: CanvasParam = {
      name: "color",
      kind: "Color" as unknown as ParamKind,
      value: "#ff8800",
    };
    const ctx: SlotContext = {
      nodeId: "n1",
      param: unknownParam,
      w: 240,
      open: false,
    };

    expect(slotControl(unknownParam)).toBe("field");
    const element = slotElement(ctx);
    const input = islandOf(element) as HTMLInputElement;
    expect(input.type).toBe("text");
  });
});
