// The context-menu commands that belong to sources (TKT-138): the rail and
// chip toggles, unlinking a chip, and promoting a path to a source. Pure of
// the DOM: the menu (nodeContextMenu.ts) shows what this returns.

import type { ChipHit, RowHit } from "./chipGeometry.js";
import type { CanvasParam } from "./canvasSlots.js";
import type { MenuItem } from "./nodeContextMenu.js";
import type { SourceView } from "./sources.js";

export interface SourceMenuDeps {
  readonly view: () => SourceView;
  readonly setView: (view: SourceView) => void;
  /** True when node `nodeId` is a source node. */
  readonly isSource: (nodeId: string) => boolean;
  /** The chip row under a world point, or null. */
  readonly chipAt: (x: number, y: number) => ChipHit | null;
  /** The parameter row under a world point, or null. */
  readonly rowAt: (x: number, y: number) => RowHit | null;
  /** "Share as source" on a path parameter. */
  readonly promote: (nodeId: string, param: string) => void;
  /** Cuts the edge a chip stands for. */
  readonly unlink: (hit: ChipHit) => void;
}

/** The commands for node `nodeId`, right-clicked at world point (x, y). */
export function sourceMenuItems(deps: SourceMenuDeps, nodeId: string, x: number, y: number): MenuItem[] {
  const view = deps.view();
  const chip = deps.chipAt(x, y);
  const row = deps.rowAt(x, y);
  const items: MenuItem[] = [];
  if (row && canShare(row.param))
    items.push({ label: "Share as source", run: () => deps.promote(row.nodeId, row.param.name) });
  if (chip) items.push({ label: `Unlink ${chip.sourceId}`, run: () => deps.unlink(chip) });
  if (deps.isSource(nodeId)) {
    items.push({
      label: view.rail ? "Show sources on the canvas" : "Move sources to the rail",
      run: () => deps.setView({ ...view, rail: !view.rail }),
    });
  }
  if (chip || deps.isSource(nodeId)) {
    items.push({
      label: view.chips ? "Draw wires instead of chips" : "Show chips instead of wires",
      run: () => deps.setView({ ...view, chips: !view.chips }),
    });
  }
  return items;
}

/** A path parameter that has a socket, holds a path, and is not yet linked. */
const canShare = (param: CanvasParam): boolean =>
  param.kind === "FilePath" && param.bindable !== undefined && param.wired === undefined && param.value.trim() !== "";
