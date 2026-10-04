import type { ColumnType } from "@bimopenflow/contracts";

export const isNumericType = (t: ColumnType): boolean =>
  t === "Integer" || t === "Number";

/** Fractional digits shown for a non-integer; the exact value stays in the cell title. */
export const MAX_FRACTION_DIGITS = 6;

/** Exact invariant text of a number: JS shortest round-trip, never locale-dependent. */
export const exactNumber = (n: number): string => String(n);

/**
 * Display text of a number: integers (and non-finite or sub-1e-6 values,
 * which toFixed would show as 0) exactly; other doubles rounded to
 * MAX_FRACTION_DIGITS with trailing zeros trimmed. Invariant, never
 * locale-dependent.
 */
export const formatNumber = (n: number): string =>
  Number.isInteger(n) || !Number.isFinite(n) || (n !== 0 && Math.abs(n) < 1e-6)
    ? exactNumber(n)
    : n.toFixed(MAX_FRACTION_DIGITS).replace(/\.?0+$/, "");

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
