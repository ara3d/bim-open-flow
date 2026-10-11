// Where a chip sits on a card (TKT-138): the row of the parameter a source
// feeds. Shared by the hover listener and the unlink menu item. Gratify-free.

import { placeSlots, SLOT_X_PAD } from "./canvasSlots.js";
import { portRowsBottom } from "./nodeSize.js";
import type { CanvasModel } from "./viewModel.js";

export interface ChipHit {
  readonly nodeId: string;
  readonly param: string;
  readonly sourceId: string;
}

/** The chip row under world point (x, y), or null. */
export function chipAt(model: Pick<CanvasModel, "nodes">, x: number, y: number): ChipHit | null {
  for (const node of [...model.nodes].reverse()) {
    if (x < node.x + SLOT_X_PAD || x > node.x + node.w - SLOT_X_PAD) continue;
    const { slots } = placeSlots(node.params, portRowsBottom(node.inputs.length, node.outputs.length));
    const slot = slots.find((s) => s.param.chip && y >= node.y + s.y && y <= node.y + s.y + s.h);
    if (slot) return { nodeId: node.id, param: slot.param.name, sourceId: slot.param.chip!.sourceId };
  }
  return null;
}
