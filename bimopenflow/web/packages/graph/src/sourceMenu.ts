// The context-menu commands that belong to sources (TKT-138): the rail and
// chip toggles, unlinking a chip, promoting a path to a source, and making a
// source's path a graph input or putting it back (TKT-204). Pure of
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
  /** The graph-input commands (TKT-204); a menu without them offers none. */
  readonly inputs?: InputMenuDeps;
}

export interface InputMenuDeps {
  /** True when source `nodeId` holds a literal path that could become a graph input. */
  readonly canMake: (nodeId: string) => boolean;
  /** "Make a graph input" on a source. */
  readonly make: (nodeId: string) => void;
  /** The parameter of `nodeId` that "Inline the graph input" would act on at this row, or null. */
  readonly inlineParam: (nodeId: string, row: RowHit | null) => string | null;
  /** "Inline the graph input" on one parameter. */
  readonly inline: (nodeId: string, param: string) => void;
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
  const inputs = deps.inputs;
  if (inputs && deps.isSource(nodeId) && inputs.canMake(nodeId))
    items.push({ label: "Make a graph input", run: () => inputs.make(nodeId) });
  const inlined = inputs?.inlineParam(nodeId, row) ?? null;
  if (inputs && inlined !== null) items.push({ label: "Inline the graph input", run: () => inputs.inline(nodeId, inlined) });
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
