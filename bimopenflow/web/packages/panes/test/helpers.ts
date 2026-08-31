import type { ColumnType, TableSlice } from "@bimopenflow/contracts";
import type { PaneContext, PaneEvent } from "../src/pane";

export const makeSlice = (
  columns: Array<[string, ColumnType]>,
  rows: unknown[][],
): TableSlice => ({
  columns: columns.map(([name, type]) => ({ name, type })),
  rows,
  totalRows: rows.length,
  skip: 0,
});

export const emptySlice: TableSlice = makeSlice([], []);

export const fakeCtx = (): PaneContext => ({
  requestTable: async () => emptySlice,
  resolveAsset: (url) => `asset:${url}`,
});

/** Collects a pane's events for assertions. */
export const collect = (): { events: PaneEvent[]; handler: (e: PaneEvent) => void } => {
  const events: PaneEvent[] = [];
  return { events, handler: (e) => events.push(e) };
};

/** Waits for queued MutationObserver callbacks (microtask + macrotask). */
export const settle = (): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, 0));
