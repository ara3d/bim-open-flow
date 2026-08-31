import type { ColumnType } from "@bimopenflow/contracts";

export const isNumericType = (t: ColumnType): boolean =>
  t === "Integer" || t === "Number";

/** Invariant number formatting: JS shortest round-trip, never locale-dependent. */
export const formatNumber = (n: number): string => String(n);

/** Invariant cell formatting; null/undefined render as the empty string. */
export const formatValue = (value: unknown, type: ColumnType): string => {
  if (value === null || value === undefined) return "";
  switch (type) {
    case "Boolean":
      return value ? "true" : "false";
    case "Integer":
    case "Number":
      return formatNumber(Number(value));
    case "Text":
      return String(value);
  }
};

/** Numeric view of a cell; null/undefined/non-numeric become NaN. */
export const numberOf = (value: unknown): number =>
  value === null || value === undefined || value === "" ? NaN : Number(value);
