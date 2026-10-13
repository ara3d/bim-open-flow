import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createChartPane, splitRowColors } from "../src/chartPane";
import { renderLegendView, scaleLegendView, parseScaleLegend } from "../src/scaleLegend";
import { conformance, tableInput } from "./conformance";
import { drawnEncoding, drawnSpec, fakeChartRenderer, type FakeChart } from "./chartFake";
import { fakeCtx, makeSlice, settle } from "./helpers";

const LEGEND_COLUMNS: Array<[string, "Text" | "Number" | "Integer"]> = [
  ["column", "Text"],
  ["domain", "Text"],
  ["role", "Text"],
  ["label", "Text"],
  ["value", "Number"],
  ["r", "Number"],
  ["g", "Number"],
  ["b", "Number"],
  ["count", "Integer"],
];

const legendSlice = (rows: unknown[][]) => makeSlice(LEGEND_COLUMNS, rows);

const numericLegendRows: unknown[][] = [
  ["meshVolume", "manual", "stop", "0", 0, 0.267, 0.005, 0.329, null],
  ["meshVolume", "manual", "stop", "1", 1, 0.993, 0.906, 0.144, null],
  ["meshVolume", "manual", "above", "> 1", 1, 0.993, 0.906, 0.144, 2],
];

conformance({
  name: "ChartPane (bar)",
  make: () => createChartPane({ chart: "bar" }),
  input: tableInput,
});
conformance({
  name: "ChartPane (line)",
  make: () => createChartPane({ chart: "line" }),
  input: tableInput,
});

describe("ChartPane", () => {
  let fake: FakeChart;
  beforeEach(() => { fake = fakeChartRenderer(); });
  afterEach(() => fake.restore());

  it("draws the bar spec of its options into the chart element", async () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "bar", categoryColumn: "name", valueColumn: "area", title: "Areas" });
    pane.mount(host, fakeCtx());
    pane.update(tableInput);
    await settle();
    const spec = drawnSpec(host)!;
    expect(host.querySelector<HTMLElement>(".bof-panes-chart")!.dataset.mark).toBe("bar");
    expect(spec.title).toEqual({ text: "Areas" });
    expect(drawnEncoding(spec).x).toMatchObject({ field: "name" });
    expect(drawnEncoding(spec).y).toMatchObject({ field: "area" });
    pane.destroy();
  });

  it("draws the line spec for a line chart and redraws on new data, finalizing the old chart", async () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "line", title: "Trend" });
    pane.mount(host, fakeCtx());
    pane.update({ kind: "table", data: makeSlice([["x", "Number"], ["y", "Number"]], [[0, 1], [1, 2]]) });
    await settle();
    expect(host.querySelector<HTMLElement>(".bof-panes-chart")!.dataset.mark).toBe("line");
    expect(drawnSpec(host)!.title).toEqual({ text: "Trend" });
    pane.update({ kind: "table", data: makeSlice([["x", "Number"], ["y", "Number"]], [[0, 5], [1, 6], [2, 7]]) });
    await settle();
    expect(fake.specs).toHaveLength(2);
    expect(fake.finalized).toBe(1);
    expect((drawnSpec(host)!.data as { values: unknown[] }).values).toHaveLength(3);
    pane.destroy();
    expect(fake.finalized).toBe(2);
  });

  it("does not redraw for the same table twice", async () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "bar" });
    pane.mount(host, fakeCtx());
    pane.update(tableInput);
    pane.update(tableInput);
    await settle();
    expect(fake.specs).toHaveLength(1);
    pane.destroy();
  });

  it("colours the bars from a table's r/g/b columns and drops them from the plotted data", async () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "bar", categoryColumn: "name" });
    pane.mount(host, fakeCtx());
    pane.update({
      kind: "table",
      data: makeSlice(
        [["name", "Text"], ["area", "Number"], ["r", "Number"], ["g", "Number"], ["b", "Number"]],
        [["a", 1, 1, 0, 0], ["b", 2, 0, 1, 0]],
      ),
    });
    await settle();
    const spec = drawnSpec(host)!;
    const rows = (spec.data as { values: Record<string, unknown>[] }).values;
    expect(rows.map((r) => r.__color)).toEqual(["rgb(255,0,0)", "rgb(0,255,0)"]);
    expect(Object.keys(rows[0]!)).not.toContain("r");
    expect(drawnEncoding(spec).color).toMatchObject({ field: "__color", scale: null });
    pane.destroy();
  });

  it("renders a legend input as the same DOM renderLegendView produces for the same slice", () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "bar" });
    pane.mount(host, fakeCtx());
    pane.update({ kind: "legend", data: legendSlice(numericLegendRows) });

    const legendEl = host.querySelector(".bof-panes-legend") as HTMLElement;
    expect(legendEl).not.toBeNull();
    expect(legendEl.hidden).toBe(false);

    const expected = document.createElement("div");
    renderLegendView(expected, scaleLegendView(parseScaleLegend(legendSlice(numericLegendRows))!));
    expect(legendEl.innerHTML).toBe(expected.innerHTML);
    pane.destroy();
  });

  it("hides the legend strip when the legend slice has no rows", () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "bar" });
    pane.mount(host, fakeCtx());
    pane.update({ kind: "legend", data: legendSlice(numericLegendRows) });
    pane.update({ kind: "legend", data: legendSlice([]) });

    const legendEl = host.querySelector(".bof-panes-legend") as HTMLElement;
    expect(legendEl.hidden).toBe(true);
    pane.destroy();
  });
});

describe("splitRowColors", () => {
  it("removes r/g/b columns and returns one rgb(...) string per row", () => {
    const data = makeSlice(
      [["name", "Text"], ["r", "Number"], ["g", "Number"], ["b", "Number"]],
      [["a", 1, 0, 0], ["b", 0, 1, 0]],
    );
    const { data: split, colors } = splitRowColors(data);
    expect(split.columns.map((c) => c.name)).toEqual(["name"]);
    expect(split.rows).toEqual([["a"], ["b"]]);
    expect(colors).toEqual(["rgb(255,0,0)", "rgb(0,255,0)"]);
  });

  it("returns colors null when r/g/b columns are absent", () => {
    const data = makeSlice([["name", "Text"]], [["a"]]);
    const { data: split, colors } = splitRowColors(data);
    expect(split).toEqual(data);
    expect(colors).toBeNull();
  });
});
