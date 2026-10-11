// The row of a bindable parameter while an edge feeds it: the inline control
// collapses to the parameter's label and a read-only preview of the wired
// value (wiredParams.wiredPreview). Painted on the canvas, with no island, so
// the native input is released (canvasEditor prunes it) and comes back with
// its stored string once the wire is cut.

import { Color, fitText, part, v, type Element } from "gratify";
import type { CanvasParam, SlotContext } from "./canvasSlots.js";
import { COMPACT_SLOT_H, SLOT_LABEL_GAP, SLOT_LABEL_SIZE, SLOT_VALUE_SIZE } from "./canvasSlots.js";
import { paramLabel } from "./numericParam.js";

interface WiredSlotProps {
  param: CanvasParam;
  w: number;
}

const WiredSlot = part<WiredSlotProps, { label: Color; value: Color }>("bof-slot-wired", {
  size: (p) => v(p.w, COMPACT_SLOT_H),
  style: (t) => ({ label: t.textDim, value: t.accent }),
  render(node, painter, style) {
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
  },
});

export function wiredSlot(ctx: SlotContext): Element {
  return WiredSlot(ctx.param.name, { param: ctx.param, w: ctx.w });
}
