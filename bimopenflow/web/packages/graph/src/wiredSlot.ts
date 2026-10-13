// The row of a bindable parameter while an edge feeds it: the inline control
// collapses to the parameter's label and a read-only preview of the wired
// value (wiredParams.wiredPreview). Painted on the canvas, with no island, so
// the native input is released (canvasEditor prunes it) and comes back with
// its stored string once the wire is cut.

import { Color, fitText, GNode, part, v, type Element, type Painter } from "gratify";
import type { CanvasParam, SlotContext } from "./canvasSlots.js";
import { COMPACT_SLOT_H, SLOT_LABEL_GAP, SLOT_LABEL_SIZE, SLOT_VALUE_SIZE } from "./canvasSlots.js";
import { paramLabel } from "./numericParam.js";

interface WiredSlotProps {
  param: CanvasParam;
  w: number;
}

type Style = { label: Color; value: Color };

const wiredStyle = (t: { textDim: Color; accent: Color }): Style => ({ label: t.textDim, value: t.accent });

/** The label on the left and the wired value's preview on the right. */
function renderRow(node: GNode<WiredSlotProps>, painter: Painter, style: Style): void {
  const r = node.rect;
  const { param } = node.props;
  const label = paramLabel(param.name, param.kind, param.control);
  const labelText = fitText(painter.measure, label, r.w / 2, SLOT_LABEL_SIZE);
  const y = r.y + r.h / 2;
  painter.label(labelText, v(r.x, y), style.label, { align: "left", size: SLOT_LABEL_SIZE });
  const room = r.w - painter.measure.text(labelText, SLOT_LABEL_SIZE).x - SLOT_LABEL_GAP;
  painter.label(fitText(painter.measure, param.wired ?? "", room, SLOT_VALUE_SIZE), v(r.right, y), style.value, {
    align: "right",
    size: SLOT_VALUE_SIZE,
  });
}

const WiredSlot = part<WiredSlotProps, Style>("bof-slot-wired", {
  size: (p) => v(p.w, COMPACT_SLOT_H),
  style: wiredStyle,
  render: renderRow,
});

export function wiredSlot(ctx: SlotContext): Element {
  return WiredSlot(ctx.param.name, { param: ctx.param, w: ctx.w });
}
