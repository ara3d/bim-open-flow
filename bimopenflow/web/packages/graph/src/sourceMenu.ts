// The context-menu commands that belong to sources (TKT-138): the rail and
// chip toggles, and, in later commits, promoting a path and unlinking a chip.
// Pure of the DOM: the menu (nodeContextMenu.ts) shows what this returns.

import type { MenuItem } from "./nodeContextMenu.js";
import type { SourceView } from "./sources.js";

export interface SourceMenuDeps {
  readonly view: () => SourceView;
  readonly setView: (view: SourceView) => void;
  /** True when node `nodeId` is a source node. */
  readonly isSource: (nodeId: string) => boolean;
}

/** The commands for node `nodeId`: on a source, where sources are drawn. */
export function sourceMenuItems(deps: SourceMenuDeps, nodeId: string): MenuItem[] {
  const view = deps.view();
  if (!deps.isSource(nodeId)) return [];
  return [{
    label: view.rail ? "Show sources on the canvas" : "Move sources to the rail",
    run: () => deps.setView({ ...view, rail: !view.rail }),
  }];
}
