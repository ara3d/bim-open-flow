import type { ColumnSchema } from "@bimopenflow/contracts";

/**
 * Index of the column called "name": an exact match first, else one ignoring
 * case. DuckDB identifiers are case-insensitive and recorded runs predate the
 * PascalCase column vocabulary (TKT-206), so "GlobalId" finds "globalId".
 */
export const columnIndex = (
  columns: readonly ColumnSchema[],
  name: string,
): number => {
  const exact = columns.findIndex((c) => c.name === name);
  if (exact >= 0) return exact;
  const lower = name.toLowerCase();
  return columns.findIndex((c) => c.name.toLowerCase() === lower);
};

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
