// Graph-node selection helpers over the state store: which selected id the
// panes follow, and keeping that choice across a reopen of the same analysis.

import type { State, Store } from "@bimopenflow/state";

/** The last selected id that is an actual graph node (panes follow it). */
export function primaryNodeId(state: State): string | null {
  const nodeIds = new Set(state.document.structure.nodes.map((n) => n.id));
  for (let i = state.selection.length - 1; i >= 0; i--)
    if (nodeIds.has(state.selection[i]!)) return state.selection[i]!;
  return null;
}

/**
 * Runs `reopen` (which replaces the store's document and clears its
 * selection) and reselects the node that was primary before, when the
 * reopened graph still has it. Returns that node id, or null when nothing
 * was restored.
 */
export async function reopenKeepingSelection(
  store: Store,
  reopen: () => Promise<void>,
): Promise<string | null> {
  const before = primaryNodeId(store.getState());
  await reopen();
  const survives = before !== null && store.getState().document.structure.nodes.some((n) => n.id === before);
  if (survives) store.dispatch({ type: "select", ids: [before] });
  return survives ? before : null;
}
