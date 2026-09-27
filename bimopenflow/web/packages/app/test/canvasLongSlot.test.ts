// Headless tests for the long-text parameter row: the painted preview, the
// Press -> openEditor intent, the anchored editor island, and its commit and
// discard paths through dispatchInline.

import { afterEach, describe, expect, it } from "vitest";
import { fitText, rect, tokens, v, type Element, type GNode } from "gratify";
import { longTextSlot, pruneLongValueEditors } from "../src/canvasLongSlot.js";
import type { CanvasIntent } from "../src/canvasIntents.js";
import type { SlotContext } from "../src/canvasSlots.js";
import { setInlineControlDispatch } from "../src/slotShared.js";

afterEach(() => pruneLongValueEditors(new Set()));

const slotNode = (element: Element): GNode<SlotContext> => ({
  key: element.key,
  props: element.props as SlotContext,
  rect: rect(0, 0, 240, 50),
  ch: { hover: 0 } as GNode<SlotContext>["ch"],
  states: new Set(),
  local: element.part.localInit,
});

const style = (element: Element) => element.part.style!(tokens, slotNode(element).ch, element.props);

interface PaintCalls {
  label: unknown[][];
  box: unknown[][];
}

function fakePainter(): { painter: any; calls: PaintCalls } {
  const calls: PaintCalls = { label: [], box: [] };
  const painter: any = {
    measure: { text: () => v(0, 0) },
    clear() {},
    box: (...args: unknown[]) => calls.box.push(args),
    label: (...args: unknown[]) => calls.label.push(args),
    dot() {},
    ring() {},
    line() {},
    wire() {},
    glow(_c: unknown, _b: unknown, draw: () => void) {
      draw();
    },
    push() {},
    pop() {},
    alpha() {},
    translate() {},
    scaleAt() {},
    screen() {},
    view() {},
  };
  return { painter, calls };
}

const ctx = (open: boolean): SlotContext => ({
  nodeId: "f",
  param: { name: "expr", kind: "Expression", value: "area > 10" },
  w: 240,
  open,
});

describe("longTextSlot (headless)", () => {
  it("closed row paints the label and a one-line preview, and its island facet is null", () => {
    const element = longTextSlot(ctx(false));
    const node = slotNode(element);
    const { painter, calls } = fakePainter();
    element.part.render!(node, painter, style(element));

    expect(calls.label.some((args) => args[0] === "Expr")).toBe(true);
    expect(calls.label.some((args) => args[0] === "area > 10")).toBe(true);
    expect(element.part.island!(node)).toBeNull();
  });

  it("cuts the preview to the box's measured width, not a fixed character count", () => {
    // Regression for defect 4: a fixed 48-character cut sat about 100px past
    // the node edge on this row's width. fitText against the box's actual
    // width (row width minus padding) should never overflow it, however the
    // font measures.
    const longValue = "area is greater than ten and level equals L1 ".repeat(4).trim();
    const wideCtx: SlotContext = { nodeId: "f", param: { name: "expr", kind: "Expression", value: longValue }, w: 240, open: false };
    const element = longTextSlot(wideCtx);
    const node = slotNode(element);
    const { painter, calls } = fakePainter();
    painter.measure.text = (s: string) => v(s.length * 8, 0); // a wide, realistic-ish font
    element.part.render!(node, painter, style(element));

    const preview = calls.label.map((args) => args[0] as string).find((s) => s !== "Expr")!;
    const maxW = node.rect.w - 14; // box width minus PREVIEW_PAD
    expect(painter.measure.text(preview, 14).x).toBeLessThanOrEqual(maxW);
    expect(preview.endsWith("…")).toBe(true);
    expect(preview).toEqual(fitText(painter.measure, longValue, maxW, 14));
    expect(preview.length).toBeLessThan(48); // shorter than the old fixed cut
  });

  it("pressing the row yields the openEditor intent", () => {
    const element = longTextSlot(ctx(false));
    const press = element.part.on!.find((i) => i.kind === "press") as { kind: "press"; to(node: GNode<SlotContext>): unknown };
    expect(press).toBeTruthy();
    expect(press.to(slotNode(element))).toEqual({ kind: "openEditor", nodeId: "f", name: "expr" });
  });

  it("open row's island is the editor wrapper, placed under the row, with the textarea holding the value", () => {
    const element = longTextSlot(ctx(true));
    const node = slotNode(element);
    const island = element.part.island!(node)!;
    expect(island).toBeTruthy();
    expect(island.rect).toEqual(rect(0, 54, 360, 200)); // r.bottom(50)+4, max(240,360)
    const textarea = (island.el as HTMLElement).querySelector("textarea")!;
    expect(textarea.value).toBe("area > 10");
  });

  it("typing a new value then Ctrl+Enter dispatches setParam then closeEditor, in that order", () => {
    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((intent) => intents.push(intent));
    const element = longTextSlot(ctx(true));
    const island = element.part.island!(slotNode(element))!;
    const textarea = (island.el as HTMLElement).querySelector("textarea") as HTMLTextAreaElement;

    textarea.value = "area > 20";
    textarea.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }));

    expect(intents).toEqual([
      { kind: "setParam", nodeId: "f", name: "expr", value: "area > 20" },
      { kind: "closeEditor" },
    ]);
  });

  it("Escape dispatches only closeEditor", () => {
    const intents: CanvasIntent[] = [];
    setInlineControlDispatch((intent) => intents.push(intent));
    const element = longTextSlot(ctx(true));
    const island = element.part.island!(slotNode(element))!;
    const textarea = (island.el as HTMLElement).querySelector("textarea") as HTMLTextAreaElement;

    textarea.value = "area > 999";
    textarea.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }));

    expect(intents).toEqual([{ kind: "closeEditor" }]);
  });

  it("prune disposes a row that is no longer live", () => {
    setInlineControlDispatch(() => {});
    const element = longTextSlot(ctx(true));
    const node = slotNode(element);
    const first = element.part.island!(node)!;

    pruneLongValueEditors(new Set()); // "f::expr" is not live

    const second = element.part.island!(node)!;
    expect(second.el).not.toBe(first.el);
    expect((second.el as HTMLElement).querySelector("textarea")!.value).toBe("area > 10");
  });
});
