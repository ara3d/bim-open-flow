import { describe, expect, it } from "vitest";
import { barChartSpec, chartSpec, COLOR_FIELD, lineChartSpec, ROW_FIELD } from "../src/chartSpec";
import { chartConfig, chartSeries } from "../src/chartTheme";
import { makeSlice } from "./helpers";

const table = makeSlice(
  [["name", "Text"], ["area", "Number"], ["count", "Integer"]],
  [["a", 1, 2], ["b", 3, 4]],
);

type Layer = { mark: { type: string }; encoding: Record<string, Record<string, unknown>> };
const layers = (spec: Record<string, unknown>) => spec.layer as Layer[];
const bars = (spec: Record<string, unknown>) => layers(spec)[0]!;

describe("barChartSpec", () => {
  it("fills its container, carries the theme, and keeps the rows in order", () => {
    const spec = barChartSpec(table, { categoryColumn: "name", valueColumn: "area" }, null);
    expect(spec.width).toBe("container");
    expect(spec.height).toBe("container");
    expect(spec.config).toBe(chartConfig);
    expect((spec.data as { values: unknown[] }).values).toEqual([
      { [ROW_FIELD]: 1, name: "a", area: 1, count: 2 },
      { [ROW_FIELD]: 2, name: "b", area: 3, count: 4 },
    ]);
    const { x, y } = bars(spec).encoding;
    expect(x).toMatchObject({ field: "name", type: "nominal", sort: null });
    expect(y).toMatchObject({ field: "area", type: "quantitative" });
  });

  it("colours a single series by category, with value labels as a second layer", () => {
    const spec = barChartSpec(table, { categoryColumn: "name", valueColumn: "area" }, null);
    expect(bars(spec).encoding.color).toMatchObject({ field: "name", legend: null });
    expect(layers(spec)[1]!.mark.type).toBe("text");
    expect(layers(spec)[1]!.encoding.text).toMatchObject({ field: "area" });
  });

  it("folds several series into grouped bars coloured by series, without labels", () => {
    const spec = barChartSpec(table, { categoryColumn: "name", seriesColumns: ["area", "count"] }, null);
    expect(spec.transform).toEqual([{ fold: ["area", "count"], as: ["series", "value"] }]);
    const { xOffset, color, y } = bars(spec).encoding;
    expect(xOffset).toMatchObject({ field: "series" });
    expect(color).toMatchObject({ field: "series", sort: ["area", "count"] });
    expect(y).toMatchObject({ field: "value" });
    expect(layers(spec)).toHaveLength(1);
  });

  it("stacks several series when the layout says so", () => {
    const spec = barChartSpec(table, { seriesColumns: ["area", "count"], layout: "stacked" }, null);
    const { xOffset, color } = bars(spec).encoding;
    expect(xOffset).toBeUndefined();
    expect(color).toMatchObject({ field: "series" });
  });

  it("swaps the axes for a horizontal layout and keeps the labels level", () => {
    const spec = barChartSpec(table, { categoryColumn: "name", valueColumn: "count", layout: "horizontal" }, null);
    const { x, y } = bars(spec).encoding;
    expect(y).toMatchObject({ field: "name", axis: { labelAngle: 0 } });
    expect(x).toMatchObject({ field: "count" });
    expect(layers(spec)[1]!.mark).toMatchObject({ type: "text", align: "left" });
  });

  it("slants the labels of a vertical chart whose categories are long", () => {
    const long = makeSlice([["name", "Text"], ["n", "Integer"]], [["installation room", 1], ["kitchen", 2]]);
    const spec = barChartSpec(long, {}, null);
    expect(bars(spec).encoding.x).toMatchObject({ axis: { labelAngle: -35 } });
  });

  it("takes the scale's colours when the table carried r g b, over any colorBy", () => {
    const spec = barChartSpec(table, { valueColumn: "area", colorBy: "none" }, ["rgb(255,0,0)", null]);
    expect(bars(spec).encoding.color).toMatchObject({ field: COLOR_FIELD, scale: null });
    expect((spec.data as { values: Record<string, unknown>[] }).values[0]![COLOR_FIELD]).toBe("rgb(255,0,0)");
  });

  it("leaves the accent to the theme for colorBy none, and by series only with several", () => {
    expect(bars(barChartSpec(table, { valueColumn: "area", colorBy: "none" }, null)).encoding.color).toBeUndefined();
    expect(bars(barChartSpec(table, { valueColumn: "area", colorBy: "series" }, null)).encoding.color).toBeUndefined();
    expect(bars(barChartSpec(table, { seriesColumns: ["area", "count"], colorBy: "category" }, null)).encoding.color)
      .toMatchObject({ field: "name" });
  });

  it("falls back to the first text column, then to the row index", () => {
    expect(bars(barChartSpec(table, { categoryColumn: "missing" }, null)).encoding.x).toMatchObject({ field: "name" });
    const numbers = makeSlice([["v", "Number"]], [[1], [2]]);
    expect(bars(barChartSpec(numbers, {}, null)).encoding.x).toMatchObject({ field: ROW_FIELD, type: "ordinal" });
  });

  it("puts the title on the spec and the palette in the config", () => {
    expect(barChartSpec(table, { title: "Areas" }, null).title).toEqual({ text: "Areas" });
    expect(barChartSpec(table, {}, null).title).toBeUndefined();
    expect(chartConfig.range.category).toEqual(chartSeries);
  });
});

describe("lineChartSpec", () => {
  it("draws one line per series over a numeric x, broken at missing values", () => {
    const spec = lineChartSpec(table, { xColumn: "area", seriesColumns: ["count"] });
    expect(spec.mark).toMatchObject({ type: "line", point: true, invalid: "break-paths-show-domains" });
    const encoding = spec.encoding as Record<string, Record<string, unknown>>;
    expect(encoding.x).toMatchObject({ field: "area", type: "quantitative" });
    expect(encoding.y).toMatchObject({ field: "count" });
    expect(encoding.color).toBeUndefined();
  });

  it("treats a text x as nominal in row order and folds several series", () => {
    const spec = lineChartSpec(table, { xColumn: "name" });
    const encoding = spec.encoding as Record<string, Record<string, unknown>>;
    expect(encoding.x).toMatchObject({ field: "name", type: "nominal", sort: null });
    expect(spec.transform).toEqual([{ fold: ["area", "count"], as: ["series", "value"] }]);
    expect(encoding.color).toMatchObject({ field: "series" });
  });

  it("uses the row index when no x column resolves", () => {
    const encoding = lineChartSpec(table, {}).encoding as Record<string, Record<string, unknown>>;
    expect(encoding.x).toMatchObject({ field: ROW_FIELD, type: "quantitative", title: null });
  });
});

describe("chartSpec", () => {
  it("dispatches on the chart kind", () => {
    expect((chartSpec(table, { chart: "line" }, null).mark as { type: string }).type).toBe("line");
    expect(bars(chartSpec(table, { chart: "bar" }, null)).mark.type).toBe("bar");
  });
});
