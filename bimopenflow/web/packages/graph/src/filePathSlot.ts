// The FilePath row: a caption above the path box, browse button, and not-found
// badge that filePathControl.ts builds. The row is a gratify part with one
// island, the same shape as the field row in canvasControls.ts.

import { Color, fitText, part, rect, v, type Element } from "gratify";
import type { SlotContext } from "./canvasSlots.js";
import { FIELD_SLOT_H, SLOT_LABEL_SIZE } from "./canvasSlots.js";
import type { CanvasParam } from "./canvasSlots.js";
import type { CanvasInstance } from "./instance.js";
import { paramLabel } from "./numericParam.js";

interface FilePathSlotProps {
  nodeId: string;
  param: CanvasParam;
  w: number;
  instance: CanvasInstance;
}

const FilePathSlot = part<FilePathSlotProps, { label: Color }>("bof-slot-filepath", {
  size: (p) => v(p.w, FIELD_SLOT_H),
  style: (t, ch) => ({ label: t.mix(t.textDim, t.text, ch.hover) }),
  render(node, painter, style) {
    const r = node.rect;
    const label = paramLabel(node.props.param.name, node.props.param.kind, node.props.param.control);
    painter.label(fitText(painter.measure, label, r.w, SLOT_LABEL_SIZE), v(r.x, r.y + 6), style.label, {
      align: "left",
      size: SLOT_LABEL_SIZE,
    });
  },
  island(node) {
    const { nodeId, param, instance } = node.props;
    if (instance.openDropdowns.size) return null;
    const r = node.rect;
    return {
      el: instance.filePaths.get(nodeId, param.name, param.value, param.suggest?.source ?? ""),
      rect: rect(r.x, r.y + 14, r.w, r.h - 16),
    };
  },
});

export function filePathSlot(ctx: SlotContext): Element {
  const { nodeId, param, w, instance } = ctx;
  return FilePathSlot(param.name, { nodeId, param, w, instance });
}
