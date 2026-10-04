// Headless tests for the long-text parameter row: the painted preview, the
// Press -> openEditor intent, the anchored editor island, and its commit and
// discard paths through dispatchInline.

import { afterEach, describe, expect, it } from "vitest";
import { rect, tokens, v, type Element, type GNode } from "gratify";
import { canvasThemes, currentCanvasTheme } from "../src/canvasTheme.js";
import { longTextSlot, pruneLongValueEditors } from "../src/canvasLongSlot.js";
import type { CanvasIntent } from "../src/canvasIntents.js";
import type { SlotContext } from "../src/canvasSlots.js";
import { createCanvasInstance, pruneInstance } from "../src/instance.js";

afterEach(() => { pruneLongValueEditors(instance, new Set()); instance.dispatch = () => {}; });
/** One mounted canvas's state for this file's slots; pruned after each test. */
const instance = createCanvasInstance({ document });


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
  instance,
});

describe("longTextSlot (headless)", () => {
  it("closed row paints the label and the preview, and its island facet is null", () => {
    const element = longTextSlot(ctx(false));
    const node = slotNode(element);
    const { painter, calls } = fakePainter();
    element.part.render!(node, painter, style(element));

    expect(calls.label.some((args) => args[0] === "Expr")).toBe(true);
    expect(calls.label.some((args) => args[0] === "area > 10")).toBe(true);
    expect(element.part.island!(node)).toBeNull();
  });

  it("wraps a value too long for one line over several, each within the box's measured width", () => {
    // Regression for defect 4: a fixed 48-character cut sat about 100px past
    // the node edge on this row's width. Wrapping against the box's actual
    // width (row width minus padding) should never overflow it, however the
    // font measures, and a value that fits in a few lines should not be cut.
    const longValue = "area is greater than ten and level equals L1 ".repeat(3).trim();
    const wideCtx: SlotContext = { nodeId: "f", param: { name: "expr", kind: "Expression", value: longValue }, w: 240, open: false, instance };
    const element = longTextSlot(wideCtx);
    const node = slotNode(element);
    const { painter, calls } = fakePainter();
    painter.measure.text = (s: string) => v(s.length * 8, 0); // a wide, realistic-ish font
    element.part.render!(node, painter, style(element));

    const maxW = node.rect.w - 14; // box width minus PREVIEW_PAD
    const previewLines = calls.label.map((args) => args[0] as string).filter((s) => s !== "Expr");
    expect(previewLines.length).toBeGreaterThan(1);
    expect(previewLines.length).toBeLessThanOrEqual(6);
    for (const line of previewLines) expect(painter.measure.text(line, 14).x).toBeLessThanOrEqual(maxW);
    expect(previewLines.join(" ")).not.toContain("…");
  });

  it("cuts a value that overruns six lines, with an ellipsis on the last one", () => {
    const longValue = "area is greater than ten and level equals L1 ".repeat(10).trim();
    const wideCtx: SlotContext = { nodeId: "f", param: { name: "expr", kind: "Expression", value: longValue }, w: 240, open: false, instance };
    const element = longTextSlot(wideCtx);
    const node = slotNode(element);
    const { painter, calls } = fakePainter();
    painter.measure.text = (s: string) => v(s.length * 8, 0);
    element.part.render!(node, painter, style(element));

    const maxW = node.rect.w - 14;
    const previewLines = calls.label.map((args) => args[0] as string).filter((s) => s !== "Expr");
    expect(previewLines.length).toBe(6);
    for (const line of previewLines) expect(painter.measure.text(line, 14).x).toBeLessThanOrEqual(maxW);
    expect(previewLines.at(-1)!.endsWith("…")).toBe(true);
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
    instance.dispatch = (intent) => intents.push(intent);
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
    instance.dispatch = (intent) => intents.push(intent);
    const element = longTextSlot(ctx(true));
    const island = element.part.island!(slotNode(element))!;
    const textarea = (island.el as HTMLElement).querySelector("textarea") as HTMLTextAreaElement;

    textarea.value = "area > 999";
    textarea.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }));

    expect(intents).toEqual([{ kind: "closeEditor" }]);
  });

  it("styles the textarea from the canvas theme without breaking its flex layout", () => {
    // Regression for design note 3: the editor's textarea used to keep
    // longValueEditor.ts's fixed dark palette instead of following the
    // canvas theme (light/dark), because nothing ever called styleIsland on
    // it. styleIsland replaces the whole cssText, so the flex sizing the
    // wrapper's layout depends on must survive the call.
    const element = longTextSlot(ctx(true));
    const island = element.part.island!(slotNode(element))!;
    const textarea = (island.el as HTMLElement).querySelector("textarea") as HTMLTextAreaElement;

    // longValueEditor.ts's own fixed dark border (#5b6472, i.e. rgb(91, 100,
    // 114)) would still pass a mere "is a border set" check, so compare
    // against the theme's actual muted color instead of the editor's
    // built-in default. jsdom re-serializes the color, so compare the parsed
    // rgb() triple rather than the exact css() string.
    const muted = canvasThemes[currentCanvasTheme()].palette.muted;
    expect(textarea.style.borderColor).toBe(`rgb(${muted.r}, ${muted.g}, ${muted.b})`);
    expect(textarea.style.flex).toBe("1 1 auto");
    expect(textarea.style.resize).toBe("none");
  });

  it("prune disposes a row that is no longer live", () => {
    instance.dispatch = () => {};
    const element = longTextSlot(ctx(true));
    const node = slotNode(element);
    const first = element.part.island!(node)!;

    pruneLongValueEditors(instance, new Set()); // "f::expr" is not live

    const second = element.part.island!(node)!;
    expect(second.el).not.toBe(first.el);
    expect((second.el as HTMLElement).querySelector("textarea")!.value).toBe("area > 10");
  });
});
