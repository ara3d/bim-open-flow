import type { TableData } from "@bimopenflow/contracts";
import { isNumericType } from "./format";

/** Index of a named column; exact match wins, then case-insensitive (the C# host resolves names case-insensitively). */
export const columnIndexByName = (data: TableData, name: string): number => {
  const exact = data.columns.findIndex((c) => c.name === name);
  if (exact >= 0) return exact;
  const lower = name.toLowerCase();
  return data.columns.findIndex((c) => c.name.toLowerCase() === lower);
};

export const firstTextColumn = (data: TableData): number =>
  data.columns.findIndex((c) => c.type === "Text");

export const firstNumericColumn = (data: TableData): number =>
  data.columns.findIndex((c) => isNumericType(c.type));

export const numericColumnIndices = (data: TableData): number[] =>
  data.columns.flatMap((c, i) => (isNumericType(c.type) ? [i] : []));

/**
 * Series column indices for a chart: named columns (unknown names and the
 * excluded column skipped); when none resolve, every numeric column except
 * the excluded one. May be empty — charts render an empty frame then.
 */
export const seriesColumnIndices = (
  data: TableData,
  names: string[] | undefined,
  exclude: number,
): number[] => {
  if (names) {
    const named = names
      .map((name) => columnIndexByName(data, name))
      .filter((i) => i >= 0 && i !== exclude);
    if (named.length > 0) return named;
  }
  return numericColumnIndices(data).filter((i) => i !== exclude);
};
