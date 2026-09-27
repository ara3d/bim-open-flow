// The slot registry: one factory per SlotControl. slotElement is the single
// place that turns a parameter row's chosen control (canvasSlots.slotControl)
// into the gratify element that draws it, replacing the old kind-by-kind
// if/switch chain in canvasControls.ts. TKT-23 adds a control by adding a
// factory here and a row in SLOT_FACTORIES, not by editing this dispatch.

import type { Element } from "gratify";
import { slotControl, type SlotContext, type SlotControl } from "./canvasSlots.js";
import { longTextSlot, pruneLongValueEditors } from "./canvasLongSlot.js";
import { rangeSlot, sliderSlot } from "./graphWidgets.js";
import {
  clearOpenDropdown,
  columnSlot,
  dropdownSlot,
  fieldSlot,
  numberSlot,
  pruneInlineControls,
  toggleSlot,
} from "./canvasControls.js";

export type SlotFactory = (ctx: SlotContext) => Element;

const sliderFactory: SlotFactory = (ctx) =>
  sliderSlot(ctx.nodeId, ctx.param, ctx.w, numberSlot(ctx, "field"));

const rangeFactory: SlotFactory = (ctx) => rangeSlot(ctx.nodeId, ctx.param, ctx.w);

/** One factory per control. */
export const SLOT_FACTORIES: Readonly<Record<SlotControl, SlotFactory>> = {
  toggle: toggleSlot,
  dropdown: dropdownSlot,
  number: numberSlot,
  columnSelect: columnSlot,
  field: fieldSlot,
  longText: longTextSlot,
  slider: sliderFactory,
  range: rangeFactory,
};

/** Clears a stale open-dropdown flag unless the control is "dropdown", then builds the row. */
export function slotElement(ctx: SlotContext): Element {
  const control = slotControl(ctx.param);
  if (control !== "dropdown") clearOpenDropdown(ctx.nodeId, ctx.param.name);
  return SLOT_FACTORIES[control](ctx);
}

/** pruneInlineControls, plus pruneLongValueEditors. */
export function pruneSlots(liveKeys: ReadonlySet<string>): void {
  pruneInlineControls(liveKeys);
  pruneLongValueEditors(liveKeys);
}

/** Disposes every per-row store, including the long-value editors: an empty
 *  live set makes pruneSlots treat every row as gone. */
export function disposeSlots(): void {
  pruneSlots(new Set());
}
