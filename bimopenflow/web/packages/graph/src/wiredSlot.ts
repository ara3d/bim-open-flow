// The row of a bindable parameter while an edge feeds it: the inline control
// collapses to the parameter's label and a read-only preview of the wired
// value (wiredParams.wiredPreview). Painted on the canvas, with no island, so
// the native input is released (canvasEditor prunes it) and comes back with
// its stored string once the wire is cut. When the wire comes from a source
// node and the canvas draws chips (TKT-138), the preview is a chip with the
// source's name, and pressing it selects the source.

import { calpha, Color, fitText, GNode, part, Press, rect, v, type Element, type Painter } from "gratify";
import type { CanvasIntent } from "./canvasIntents.js";
import type { CanvasParam, SlotContext } from "./canvasSlots.js";
import { COMPACT_SLOT_H, SLOT_LABEL_GAP, SLOT_LABEL_SIZE, SLOT_VALUE_SIZE } from "./canvasSlots.js";
import { paramLabel } from "./numericParam.js";

interface WiredSlotProps {
  param: CanvasParam;
  w: number;
}

type Style = { label: Color; value: Color };

/** Space inside a chip either side of its text, its corner radius, and its height. */
const CHIP_PAD = 7;
const CHIP_RADIUS = 9;
const CHIP_H = 20;

const wiredStyle = (t: { textDim: Color; accent: Color }): Style => ({ label: t.textDim, value: t.accent });

/** The label on the left and the value (a chip when the param has one) on the right. */
function renderRow(node: GNode<WiredSlotProps>, painter: Painter, style: Style): void {
  const r = node.rect;
  const { param } = node.props;
  const label = paramLabel(param.name, param.kind, param.control);
  const labelText = fitText(painter.measure, label, r.w / 2, SLOT_LABEL_SIZE);
  const y = r.y + r.h / 2;
  painter.label(labelText, v(r.x, y), style.label, { align: "left", size: SLOT_LABEL_SIZE });
  const room = r.w - painter.measure.text(labelText, SLOT_LABEL_SIZE).x - SLOT_LABEL_GAP;
  if (!param.chip) {
    painter.label(fitText(painter.measure, param.wired ?? "", room, SLOT_VALUE_SIZE), v(r.right, y), style.value, {
      align: "right",
      size: SLOT_VALUE_SIZE,
    });
    return;
  }
  const text = fitText(painter.measure, param.chip.name, room - 2 * CHIP_PAD, SLOT_VALUE_SIZE);
  const width = painter.measure.text(text, SLOT_VALUE_SIZE).x + 2 * CHIP_PAD;
  painter.box(rect(r.right - width, y - CHIP_H / 2, width, CHIP_H), CHIP_RADIUS, calpha(style.value, 0.16), style.value, 1);
  painter.label(text, v(r.right - CHIP_PAD, y), style.value, { align: "right", size: SLOT_VALUE_SIZE });
}

const WiredSlot = part<WiredSlotProps, Style>("bof-slot-wired", {
  size: (p) => v(p.w, COMPACT_SLOT_H),
  style: wiredStyle,
  render: renderRow,
});

/** The same row for a parameter fed by a source: pressing it selects the source. A
 *  separate part, so a plain wired row still lets a press start a node drag. */
const ChipSlot = part<WiredSlotProps, Style>("bof-slot-chip", {
  size: (p) => v(p.w, COMPACT_SLOT_H),
  style: wiredStyle,
  render: renderRow,
  on: [
    Press((node: GNode<WiredSlotProps>) =>
      ({ kind: "selectNode", id: node.props.param.chip!.sourceId }) satisfies CanvasIntent),
  ],
});

export function wiredSlot(ctx: SlotContext): Element {
  return (ctx.param.chip ? ChipSlot : WiredSlot)(ctx.param.name, { param: ctx.param, w: ctx.w });
}
