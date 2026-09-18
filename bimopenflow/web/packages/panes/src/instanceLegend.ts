// Legend derived from a coloured instance table (view3d.color output): the
// distinct values of the colour-driving column with their colours and row
// counts, capped so a long-tail column stays readable. Pure; no DOM.
import type { ColumnSchema, TableSlice } from "@bimopenflow/contracts";
import { columnIndex } from "./columns";
import type { LegendEntry } from "./toolkitRecipe";

/** Columns that drive colouring, most specific first: a check verdict, else the category. */
export const LEGEND_COLUMNS = ["verdict", "category"] as const;

export const LEGEND_MAX_ENTRIES = 12;

export interface InstanceLegend {
  /** Most frequent first; ties by name. */
  readonly entries: readonly LegendEntry[];
  /** Distinct values left out by the cap. */
  readonly omitted: number;
}

export const emptyInstanceLegend: InstanceLegend = { entries: [], omitted: 0 };

/** Index of the first LEGEND_COLUMNS member present, or -1. */
export const legendColumn = (columns: readonly ColumnSchema[]): number =>
  LEGEND_COLUMNS.map((name) => columnIndex(columns, name)).find((i) => i >= 0) ?? -1;

const labelOf = (value: unknown): string =>
  value === null || value === undefined || value === "" ? "(blank)" : String(value);

/**
 * One entry per distinct value of the legend column, coloured by the first
 * row carrying that value. Empty when the table has no legend column or no
 * r/g/b columns.
 */
export const legendFromSlice = (slice: TableSlice, max = LEGEND_MAX_ENTRIES): InstanceLegend => {
  const valueIdx = legendColumn(slice.columns);
  const rgb = ["r", "g", "b"].map((name) => columnIndex(slice.columns, name));
  if (valueIdx < 0 || rgb.some((i) => i < 0)) return emptyInstanceLegend;
  const seen = new Map<string, LegendEntry>();
  for (const row of slice.rows) {
    const name = labelOf(row[valueIdx]);
    const entry = seen.get(name);
    if (entry) seen.set(name, { ...entry, count: entry.count + 1 });
    else seen.set(name, { name, color: [Number(row[rgb[0]!]), Number(row[rgb[1]!]), Number(row[rgb[2]!])], count: 1 });
  }
  const sorted = [...seen.values()].sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
  return { entries: sorted.slice(0, max), omitted: Math.max(0, sorted.length - max) };
};
