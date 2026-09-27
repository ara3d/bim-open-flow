// A legend strip parsed from a legend table (view.colormap, view3d.color,
// chart.bar `legend` port) or built from the existing table-derived category
// legend. One renderer, renderLegendView, draws either kind so the 3D pane
// and the chart pane show byte-identical DOM for the same input.
import type { TableSlice } from "@bimopenflow/contracts";
import { columnIndex } from "./columns";
import { LEGEND_MAX_ENTRIES } from "./instanceLegend";
import type { LegendEntry } from "./toolkitRecipe";

export type Rgb3 = readonly [number, number, number];
export type ScaleRole = "stop" | "category" | "below" | "above" | "missing";
export type ScaleDomain = "auto" | "manual" | "categorical";

export interface ScaleRow {
  readonly role: ScaleRole;
  readonly label: string;
  readonly value: number | null;
  readonly color: Rgb3;
  readonly count: number | null;
}

export interface ScaleLegend {
  readonly column: string;
  readonly domain: ScaleDomain;
  readonly rows: readonly ScaleRow[];
}

/** What a legend strip shows, whatever produced it. */
export interface LegendView {
  readonly caption?: string;
  readonly gradient: readonly { readonly label: string; readonly color: Rgb3 }[];
  readonly chips: readonly { readonly text: string; readonly color: Rgb3 }[];
  readonly omitted: number;
}

const LEGEND_COLUMN_NAMES = ["column", "domain", "role", "label", "value", "r", "g", "b", "count"] as const;

/** Null when the slice has no rows or lacks a legend column. */
export const parseScaleLegend = (slice: TableSlice): ScaleLegend | null => {
  if (slice.rows.length === 0) return null;
  const idx = Object.fromEntries(
    LEGEND_COLUMN_NAMES.map((name) => [name, columnIndex(slice.columns, name)]),
  ) as Record<(typeof LEGEND_COLUMN_NAMES)[number], number>;
  if (Object.values(idx).some((i) => i < 0)) return null;

  const numberOrNull = (value: unknown): number | null =>
    value === null || value === undefined ? null : Number(value);

  const first = slice.rows[0]!;
  const rows: ScaleRow[] = slice.rows.map((row) => ({
    role: row[idx.role] as ScaleRole,
    label: String(row[idx.label]),
    value: numberOrNull(row[idx.value]),
    color: [Number(row[idx.r]), Number(row[idx.g]), Number(row[idx.b])] as Rgb3,
    count: numberOrNull(row[idx.count]),
  }));
  return {
    column: String(first[idx.column]),
    domain: String(first[idx.domain]) as ScaleDomain,
    rows,
  };
};

/**
 * Turns a parsed scale legend into a strip view: a gradient ramp from the
 * stop rows (labelled only at its first and last stop) plus chips for every
 * other row (below/above/missing, or category/missing). max caps the number
 * of chips shown; the rest are counted in omitted.
 */
export const scaleLegendView = (legend: ScaleLegend, max = LEGEND_MAX_ENTRIES): LegendView => {
  const stops = legend.rows.filter((row) => row.role === "stop");
  const gradient = stops.map((stop, i) => ({
    label: i === 0 || i === stops.length - 1 ? stop.label : "",
    color: stop.color,
  }));
  const chipRows = legend.rows.filter((row) => row.role !== "stop");
  const shown = chipRows.slice(0, max);
  const omitted = Math.max(0, chipRows.length - max);
  const chips = shown.map((row) => ({ text: `${row.label} (${row.count ?? 0})`, color: row.color }));
  const caption =
    legend.domain === "categorical"
      ? "category"
      : stops.length > 0
        ? `${legend.column} · ${legend.domain} domain ${stops[0]!.label} – ${stops[stops.length - 1]!.label}`
        : legend.column;
  return { caption, gradient, chips, omitted };
};

/** Keeps today's chip text: `${name} (${count} objects)`. */
export const entriesLegendView = (entries: readonly LegendEntry[], omitted: number): LegendView => ({
  gradient: [],
  chips: entries.map((entry) => ({
    text: `${entry.name} (${entry.count.toLocaleString()} objects)`,
    color: entry.color as Rgb3,
  })),
  omitted,
});

const cssColor = (color: Rgb3): string => `rgb(${color.map((c) => Math.round(c * 255)).join(",")})`;

/** Replaces el's children; hides el when the view is empty. The same view gives the same DOM. */
export const renderLegendView = (el: HTMLElement, view: LegendView): void => {
  const doc = el.ownerDocument;
  el.replaceChildren();

  if (view.caption) {
    const caption = doc.createElement("div");
    caption.className = "bof-panes-legend-caption";
    caption.textContent = view.caption;
    el.append(caption);
  }

  if (view.gradient.length > 0) {
    const ramp = doc.createElement("div");
    ramp.className = "bof-panes-legend-ramp";
    const bar = doc.createElement("div");
    bar.className = "bof-panes-legend-ramp-bar";
    const last = Math.max(1, view.gradient.length - 1);
    const stops = view.gradient.map((stop, i) => `${cssColor(stop.color)} ${(i / last) * 100}%`);
    bar.style.background = `linear-gradient(to right, ${stops.join(", ")})`;
    const labels = doc.createElement("div");
    labels.className = "bof-panes-legend-ramp-labels";
    for (const stop of view.gradient) {
      const label = doc.createElement("span");
      label.textContent = stop.label;
      labels.append(label);
    }
    ramp.append(bar, labels);
    el.append(ramp);
  }

  for (const chip of view.chips) {
    const item = doc.createElement("span");
    item.className = "bof-panes-legend-chip";
    const swatch = doc.createElement("i");
    swatch.style.background = cssColor(chip.color);
    item.append(swatch, chip.text);
    el.append(item);
  }

  if (view.omitted > 0) {
    const more = doc.createElement("span");
    more.className = "bof-panes-legend-more";
    more.textContent = `and ${view.omitted.toLocaleString()} more`;
    el.append(more);
  }

  el.hidden = el.children.length === 0;
};
