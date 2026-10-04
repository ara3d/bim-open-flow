// The node the pane area shows, as the editor hands it to a pane's feed.

import type { NodeDescriptor, NodeState, TableSlice } from "@bimopenflow/contracts";

/**
 * A result a pane can draw from the open document alone, before or without the
 * host evaluating it (the 3D pane's view recipe, PaneRegistration.preview).
 */
export type LiveViewRecipe =
  | { kind: "ready"; data: TableSlice }
  | { kind: "unsupported" }
  | { kind: "invalid"; message: string };

export interface ShownNode {
  nodeId: string;
  desc: NodeDescriptor | undefined;
  values: Record<string, string>;
  state: NodeState | undefined;
  /** Model file path feeding this node (see modelRef.modelPathFor); lets the
   * 3D pane load the model behind the instance/box tables. */
  modelPath?: string;
  pending?: boolean;
  live?: LiveViewRecipe;
  lineage?: string;
  /** True when this node is the flow's answer (TKT-46/TKT-81): the panes
   *  display it whether or not anything is selected. False means it is shown
   *  because of an explicit "show" request (double-click, the header's pin),
   *  not because it is the answer. */
  default?: boolean;
  /** True while the pane is pinned to this node regardless of the answer. */
  pinned?: boolean;
}
