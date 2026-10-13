// Where a parameter row sits on a card: the path row "Share as source" starts
// from (TKT-138) and the row a graph-input command acts on (TKT-204).
// Gratify-free, used by the context menu.

import { placeSlots, SLOT_X_PAD, type CanvasParam } from "./canvasSlots.js";
import { portRowsBottom } from "./nodeSize.js";
import type { CanvasModel } from "./viewModel.js";

export interface RowHit {
  readonly nodeId: string;
  readonly param: CanvasParam;
}

/** The parameter row under world point (x, y), or null. */
export function rowAt(model: Pick<CanvasModel, "nodes">, x: number, y: number): RowHit | null {
  for (const node of [...model.nodes].reverse()) {
    if (x < node.x + SLOT_X_PAD || x > node.x + node.w - SLOT_X_PAD) continue;
    const { slots } = placeSlots(node.params, portRowsBottom(node.inputs.length, node.outputs.length));
    const slot = slots.find((s) => y >= node.y + s.y && y <= node.y + s.y + s.h);
    if (slot) return { nodeId: node.id, param: slot.param };
  }
  return null;
}
