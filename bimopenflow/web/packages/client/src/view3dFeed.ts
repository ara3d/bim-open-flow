// How a node's result feeds the 3D pane, shared by the editor's pane area and
// the notebook's 3D embed: the model first, as "model:<id>" served in BOS,
// then the node's table as a view recipe, boxes, or instances.

import type { TableSlice } from "@bimopenflow/contracts";
import { isBoxTable } from "@bimopenflow/panes";

/** The 3D pane update kinds that carry a table. */
export type View3dDataKind = "view" | "boxes" | "instances";

/** Which 3D update a node's output table is: its port name decides, else its columns. */
export const view3dDataKind = (port: string, data: TableSlice): View3dDataKind =>
  port === "view" ? "view" : port === "boxes" || isBoxTable(data.columns) ? "boxes" : "instances";

/** The pane's url for a node's model file given its catalog id, or throws a sentence naming the fix. */
export function modelUrlFor(path: string, id: string | null): string {
  if (!id) throw new Error(`Model is not in the host catalog: ${path}. Add its directory to ModelRoots.`);
  return `model:${id}`;
}
