// A chart.* node's table and params as a Vega-Lite spec: a pure function the
// chart pane renders (chartRender.ts) and a test can compare. Bars come in
// three layouts and colour by category, by series, by a scale's r g b
// columns, or not at all; lines take one x column and any number of series.
// Rows keep the order the node gave them (the node sorts), and a missing
// value stays missing: a bar is absent and a line breaks.

import type { TableSlice } from "@bimopenflow/contracts";
import { columnIndexByName, firstTextColumn, isNumericType, seriesColumnIndices } from "@bimopenflow/viz";
import { chartConfig } from "./chartTheme";
import { columnIndex } from "./columns";

export type BarLayout = "vertical" | "horizontal" | "stacked";
export type BarColorBy = "auto" | "category" | "series" | "none";

export interface BarChartOptions {
  /** Chart title, drawn above the plot. */
  title?: string;
  /** Text column supplying the categories; the first Text column when unknown, else the row index. */
  categoryColumn?: string;
  /** One numeric value column; ignored when seriesColumns is set. */
  valueColumn?: string;
  /** Numeric columns drawn as one bar per series per category; every numeric column except the category by default. */
  seriesColumns?: string[];
  /** Vertical (grouped when several series), horizontal, or stacked. */
  layout?: BarLayout;
  /** What the bar colours encode; auto is category for one series and series for several. */
  colorBy?: BarColorBy;
}

export interface LineChartOptions {
  title?: string;
  /** Column for x: quantitative when numeric, else nominal in row order; the row index when unknown. */
  xColumn?: string;
  /** Numeric columns drawn as lines; every numeric column except x by default. */
  seriesColumns?: string[];
}

/** Chart choice plus that chart's options. A notebook stores this object with a chart embed. */
export type ChartPaneOptions =
  | ({ chart: "bar" } & BarChartOptions)
  | ({ chart: "line" } & LineChartOptions);

/** A Vega-Lite top-level spec, untyped: the renderer hands it to vega-embed as is. */
export type ChartSpec = Record<string, unknown>;

/** The field a per-row colour from a scale's r g b columns lives in. */
export const COLOR_FIELD = "__color";
/** The field holding the 1-based row index when a chart has no category or x column. */
export const ROW_FIELD = "__row";
const SERIES_FIELD = "series";
const VALUE_FIELD = "value";

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
  const keep = data.columns.map((_, i) => i).filter((i) => i !== rIdx && i !== gIdx && i !== bIdx);
  const columns = keep.map((i) => data.columns[i]!);
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

const resolveColumn = (data: TableSlice, name: string | undefined): number =>
  name ? columnIndexByName(data, name) : -1;

/** The rows as objects keyed by column name, with the row index and, when given, the colour. */
const values = (data: TableSlice, colors: (string | null)[] | null): Record<string, unknown>[] =>
  data.rows.map((row, i) => {
    const value: Record<string, unknown> = { [ROW_FIELD]: i + 1 };
    data.columns.forEach((c, j) => { value[c.name] = row[j] ?? null; });
    if (colors) value[COLOR_FIELD] = colors[i] ?? null;
    return value;
  });

const barSeries = (data: TableSlice, options: BarChartOptions, category: number): string[] => {
  const names = options.seriesColumns ?? (options.valueColumn ? [options.valueColumn] : undefined);
  return seriesColumnIndices(data, names, category).map((i) => data.columns[i]!.name);
};

/** Longest category label, to slant the labels of a vertical chart that would overlap. */
const longestLabel = (rows: Record<string, unknown>[], field: string): number =>
  Math.max(0, ...rows.map((r) => String(r[field] ?? "").length));

const frame = (spec: ChartSpec, title: string | undefined): ChartSpec => ({
  $schema: "https://vega.github.io/schema/vega-lite/v6.json",
  width: "container",
  height: "container",
  autosize: { type: "fit", contains: "padding" },
  ...(title ? { title: { text: title } } : {}),
  ...spec,
  config: chartConfig,
});

const barColorMode = (options: BarChartOptions, series: string[], colored: boolean): BarColorBy | "scale" => {
  if (colored && series.length === 1) return "scale";
  const mode = options.colorBy ?? "auto";
  return mode === "auto" ? (series.length > 1 ? "series" : "category") : mode;
};

/** Bar chart spec; see the module comment for the layouts and colours. */
export function barChartSpec(
  data: TableSlice,
  options: BarChartOptions,
  colors: (string | null)[] | null,
): ChartSpec {
  let category = resolveColumn(data, options.categoryColumn);
  if (category < 0) category = firstTextColumn(data);
  const categoryField = category >= 0 ? data.columns[category]!.name : ROW_FIELD;
  const series = barSeries(data, options, category);
  const rows = values(data, colors);
  const layout = options.layout ?? "vertical";
  const horizontal = layout === "horizontal";
  const many = series.length > 1;
  const mode = barColorMode(options, series, colors !== null);

  const categoryAxis = {
    field: categoryField,
    type: category >= 0 ? "nominal" : "ordinal",
    sort: null,
    title: null,
    axis: horizontal ? { labelAngle: 0 } : { labelAngle: longestLabel(rows, categoryField) > 8 ? -35 : 0 },
  };
  // Counts are the common case: no half ticks on the value axis.
  const valueAxis = many
    ? { field: VALUE_FIELD, type: "quantitative", title: null, axis: { tickMinStep: 1 } }
    : { field: series[0] ?? ROW_FIELD, type: "quantitative", title: series[0] ?? null, axis: { tickMinStep: 1 } };
  const color =
    mode === "scale" ? { field: COLOR_FIELD, type: "nominal", scale: null, legend: null }
    : mode === "category" ? { field: categoryField, type: "nominal", legend: null, sort: null }
    : mode === "series" && many ? { field: SERIES_FIELD, type: "nominal", title: null, sort: series }
    : undefined;
  const offset = many && layout !== "stacked" ? { field: SERIES_FIELD, sort: series } : undefined;

  const encoding: Record<string, unknown> = horizontal
    ? { y: categoryAxis, x: valueAxis, ...(offset ? { yOffset: offset } : {}) }
    : { x: categoryAxis, y: valueAxis, ...(offset ? { xOffset: offset } : {}) };
  if (color) encoding.color = color;
  encoding.tooltip = many
    ? [{ field: categoryField, type: "nominal" }, { field: SERIES_FIELD, type: "nominal" }, { field: VALUE_FIELD, type: "quantitative" }]
    : [{ field: categoryField, type: "nominal" }, ...series.map((s) => ({ field: s, type: "quantitative" }))];

  const bars = { mark: { type: "bar" }, encoding };
  // Value labels on the bars of a single series; several series would overlap.
  const labels = many
    ? []
    : [{
        mark: horizontal
          ? { type: "text", align: "left", baseline: "middle", dx: 4 }
          : { type: "text", align: "center", baseline: "bottom", dy: -3 },
        encoding: {
          ...(horizontal ? { y: categoryAxis, x: valueAxis } : { x: categoryAxis, y: valueAxis }),
          text: { field: series[0] ?? ROW_FIELD, type: "quantitative", format: ",.6~f" },
        },
      }];
  return frame({
    data: { values: rows },
    ...(many ? { transform: [{ fold: series, as: [SERIES_FIELD, VALUE_FIELD] }] } : {}),
    layer: [bars, ...labels],
  }, options.title);
}

/** Line chart spec: one line per series over x, broken where a value is missing. */
export function lineChartSpec(data: TableSlice, options: LineChartOptions): ChartSpec {
  const x = resolveColumn(data, options.xColumn);
  const xField = x >= 0 ? data.columns[x]!.name : ROW_FIELD;
  const xNumeric = x < 0 || isNumericType(data.columns[x]!.type);
  const series = seriesColumnIndices(data, options.seriesColumns, x).map((i) => data.columns[i]!.name);
  const rows = values(data, null);
  const many = series.length > 1;
  const xAxis = xNumeric
    ? { field: xField, type: "quantitative", title: x >= 0 ? xField : null }
    : { field: xField, type: "nominal", sort: null, title: xField };
  const yAxis = many
    ? { field: VALUE_FIELD, type: "quantitative", title: null, axis: { tickMinStep: 1 } }
    : { field: series[0] ?? ROW_FIELD, type: "quantitative", title: series[0] ?? null, axis: { tickMinStep: 1 } };
  const encoding: Record<string, unknown> = { x: xAxis, y: yAxis };
  if (many) encoding.color = { field: SERIES_FIELD, type: "nominal", title: null, sort: series };
  encoding.tooltip = many
    ? [{ field: xField }, { field: SERIES_FIELD, type: "nominal" }, { field: VALUE_FIELD, type: "quantitative" }]
    : [{ field: xField }, ...series.map((s) => ({ field: s, type: "quantitative" }))];
  return frame({
    data: { values: rows },
    ...(many ? { transform: [{ fold: series, as: [SERIES_FIELD, VALUE_FIELD] }] } : {}),
    mark: { type: "line", point: true, invalid: "break-paths-show-domains" },
    encoding,
  }, options.title);
}

/** The spec for a pane's options over a table whose r g b columns, if any, are already split off. */
export const chartSpec = (
  data: TableSlice,
  options: ChartPaneOptions,
  colors: (string | null)[] | null,
): ChartSpec =>
  options.chart === "line" ? lineChartSpec(data, options) : barChartSpec(data, options, colors);
