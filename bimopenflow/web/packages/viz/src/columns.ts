import type { TableData } from "@bimopenflow/contracts";
import { isNumericType } from "./format";

export const columnIndexByName = (data: TableData, name: string): number =>
  data.columns.findIndex((c) => c.name === name);

export const firstTextColumn = (data: TableData): number =>
  data.columns.findIndex((c) => c.type === "Text");

export const firstNumericColumn = (data: TableData): number =>
  data.columns.findIndex((c) => isNumericType(c.type));

export const numericColumnIndices = (data: TableData): number[] =>
  data.columns.flatMap((c, i) => (isNumericType(c.type) ? [i] : []));

/** Resolve a named column, or fall back; throws when neither exists. */
export const resolveColumn = (
  data: TableData,
  name: string | undefined,
  fallback: (data: TableData) => number,
  role: string,
): number => {
  const i = name !== undefined ? columnIndexByName(data, name) : fallback(data);
  if (i < 0)
    throw new Error(
      name !== undefined
        ? `bof-viz: ${role} column "${name}" not found`
        : `bof-viz: no suitable ${role} column in table`,
    );
  return i;
};
