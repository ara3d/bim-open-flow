// Where a parameter row sits on a card (TKT-138): the chip a source feeds, and
// the path row "Share as source" starts from. Gratify-free, shared by the hover
// listener and the context menu.

import { placeSlots, SLOT_X_PAD, type CanvasParam } from "./canvasSlots.js";
import { portRowsBottom } from "./nodeSize.js";
import type { CanvasModel } from "./viewModel.js";

export interface RowHit {
  readonly nodeId: string;
  readonly param: CanvasParam;
}

export interface ChipHit {
  readonly nodeId: string;
  readonly param: string;
  readonly sourceId: string;
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

/** The chip row under world point (x, y), or null. */
export function chipAt(model: Pick<CanvasModel, "nodes">, x: number, y: number): ChipHit | null {
  const hit = rowAt(model, x, y);
  return hit?.param.chip ? { nodeId: hit.nodeId, param: hit.param.name, sourceId: hit.param.chip.sourceId } : null;
}
