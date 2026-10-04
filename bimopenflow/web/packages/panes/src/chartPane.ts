import type { ColumnSchema, TableSlice } from "@bimopenflow/contracts";
import {
  BarChart,
  LineChart,
  type BarChartOptions,
  type LineChartOptions,
} from "@bimopenflow/viz";
import type { Pane } from "./pane";
import { definePane } from "./base";
import { columnIndex } from "./columns";
import { parseScaleLegend, renderLegendView, scaleLegendView } from "./scaleLegend";

/** Chart choice plus the chosen chart's viz options, passed through as-is. */
export type ChartPaneOptions =
  | ({ chart: "bar" } & BarChartOptions)
  | ({ chart: "line" } & LineChartOptions);

const cssColor = (r: unknown, g: unknown, b: unknown): string =>
  `rgb(${[r, g, b].map((v) => Math.round(Number(v) * 255)).join(",")})`;

/** Splits r/g/b columns off a chart table: the rest for the chart, one "rgb(…)" per row; colors null when absent. */
export const splitRowColors = (
  data: TableSlice,
): { data: TableSlice; colors: (string | null)[] | null } => {
  const rIdx = columnIndex(data.columns, "r");
  const gIdx = columnIndex(data.columns, "g");
  const bIdx = columnIndex(data.columns, "b");
  if (rIdx < 0 || gIdx < 0 || bIdx < 0) return { data, colors: null };
  const keep = data.columns
    .map((_, i) => i)
    .filter((i) => i !== rIdx && i !== gIdx && i !== bIdx);
  const columns: ColumnSchema[] = keep.map((i) => data.columns[i]!);
  const rows = data.rows.map((row) => keep.map((i) => row[i]));
  const colors = data.rows.map((row) => {
    const r = row[rIdx];
    const g = row[gIdx];
    const b = row[bIdx];
    return r === null || r === undefined || g === null || g === undefined || b === null || b === undefined
      ? null
      : cssColor(r, g, b);
  });
  return { data: { ...data, columns, rows }, colors };
};

/** Smallest chart width, the viz default; a narrower pane scrolls. */
const MIN_CHART_WIDTH = 480;
/** Chart height when sized to the pane: room for slanted category labels. */
const FITTED_CHART_HEIGHT = 360;

/**
 * Chart size for a pane of the given width: the pane's width (at least
 * MIN_CHART_WIDTH), or nothing when the pane has no layout yet (a detached
 * host), leaving the viz defaults. Width and height set in the options win.
 */
export const fitToPane = (paneWidth: number): { width?: number; height?: number } =>
  paneWidth > 0
    ? { width: Math.max(MIN_CHART_WIDTH, Math.floor(paneWidth)), height: FITTED_CHART_HEIGHT }
    : {};

/**
 * Chart pane: wraps the viz BarChart or LineChart. A "table" input fills bar
 * charts from the table's r/g/b columns, if present, and drops them from the
 * plotted data. A "legend" input draws the shared strip below the chart, the
 * same way the 3D pane does; an empty legend hides it.
 * Inputs: "table" renders/updates the chart; "legend" draws the strip;
 * everything else is ignored. Emits no events.
 */
export const createChartPane = (options: ChartPaneOptions): Pane =>
  definePane((root) => {
    const chartRoot = root.ownerDocument.createElement("div");
    root.append(chartRoot);
    const legend = root.ownerDocument.createElement("div");
    legend.className = "bof-panes-legend";
    legend.setAttribute("aria-label", "Scale legend");
    root.append(legend);

    let handle: { update(data: TableSlice): void; destroy(): void } | null =
      null;
    // Mutated in place so a later "table" update can change the fills of an
    // already-mounted bar chart without remounting it (BarChart re-reads
    // options on every render call).
    let barOptions: BarChartOptions | null = null;

    const mountChart = (data: TableSlice, colors: (string | null)[] | null) => {
      const sized = { ...fitToPane(root.clientWidth), ...options };
      if (sized.chart === "bar") {
        barOptions = { ...sized, barColors: colors ?? undefined } as BarChartOptions;
        return BarChart.mount(chartRoot, data, barOptions);
      }
      return LineChart.mount(chartRoot, data, sized);
    };

    const showLegend = (data: TableSlice) => {
      const parsed = parseScaleLegend(data);
      if (!parsed) {
        legend.replaceChildren();
        legend.hidden = true;
        return;
      }
      renderLegendView(legend, scaleLegendView(parsed));
    };

    return {
      update(input) {
        if (input.kind === "table") {
          const { data, colors } = splitRowColors(input.data);
          if (handle) {
            if (barOptions) barOptions.barColors = colors ?? undefined;
            handle.update(data);
          } else {
            handle = mountChart(data, colors);
          }
          return;
        }
        if (input.kind === "legend") showLegend(input.data);
      },
      destroy() {
        handle?.destroy();
      },
    };
  });
