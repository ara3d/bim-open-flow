import type { ColumnSchema } from "@bimopenflow/contracts";

export const columnIndex = (
  columns: readonly ColumnSchema[],
  name: string,
): number => columns.findIndex((c) => c.name === name);

/**
 * The selection-id column heuristic shared by table and verdict panes:
 * "globalId" if present, else "entityId", else the first column.
 */
export const idColumnIndex = (columns: readonly ColumnSchema[]): number => {
  const g = columnIndex(columns, "globalId");
  if (g >= 0) return g;
  const e = columnIndex(columns, "entityId");
  return e >= 0 ? e : 0;
};

/** Id text of a cell: plain String conversion, empty for null/undefined. */
export const idOf = (value: unknown): string =>
  value === null || value === undefined ? "" : String(value);
