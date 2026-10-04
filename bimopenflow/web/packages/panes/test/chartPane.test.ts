import { describe, expect, it } from "vitest";
import { createChartPane, fitToPane, splitRowColors } from "../src/chartPane";
import { renderLegendView, scaleLegendView, parseScaleLegend } from "../src/scaleLegend";
import { conformance, tableInput } from "./conformance";
import { fakeCtx, makeSlice } from "./helpers";

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
  it("renders a bar chart with column options passed through", () => {
    const host = document.createElement("div");
    const pane = createChartPane({
      chart: "bar",
      categoryColumn: "name",
      valueColumn: "area",
    });
    pane.mount(host, fakeCtx());
    pane.update(tableInput);
    expect(host.querySelector("svg.bof-viz-bar-chart")).not.toBeNull();
    expect(host.querySelectorAll("rect.bof-viz-bar").length).toBe(2);
    pane.destroy();
  });

  it("passes title and seriesColumns through to the bar chart", () => {
    const host = document.createElement("div");
    const pane = createChartPane({
      chart: "bar",
      title: "Areas",
      categoryColumn: "name",
      seriesColumns: ["area", "count"],
    });
    pane.mount(host, fakeCtx());
    pane.update({
      kind: "table",
      data: makeSlice(
        [["name", "Text"], ["area", "Number"], ["count", "Integer"]],
        [["a", 1, 2], ["b", 3, 4]],
      ),
    });
    expect(host.querySelector("text.bof-viz-title")?.textContent).toBe("Areas");
    const bars = [...host.querySelectorAll("rect.bof-viz-bar")];
    expect(bars.map((b) => b.getAttribute("data-series"))).toEqual([
      "area", "count", "area", "count",
    ]);
    pane.destroy();
  });

  it("passes title through to the line chart", () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "line", title: "Trend" });
    pane.mount(host, fakeCtx());
    pane.update({
      kind: "table",
      data: makeSlice([["y", "Number"]], [[1], [2]]),
    });
    expect(host.querySelector("text.bof-viz-title")?.textContent).toBe("Trend");
    pane.destroy();
  });

  it("renders a line chart and updates in place", () => {
    const host = document.createElement("div");
    const pane = createChartPane({ chart: "line" });
    pane.mount(host, fakeCtx());
    const data = makeSlice(
      [["x", "Number"], ["y", "Number"]],
      [[0, 1], [1, 2]],
    );
    pane.update({ kind: "table", data });
    expect(host.querySelectorAll("path.bof-viz-line").length).toBeGreaterThan(0);
    pane.update({
      kind: "table",
      data: makeSlice([["x", "Number"], ["y", "Number"]], [[0, 5], [1, 6], [2, 7]]),
    });
    expect(host.querySelectorAll("svg.bof-viz-line-chart").length).toBe(1);
    pane.destroy();
  });

  it("passes a table's r/g/b columns to the bar chart as fills and drops them from the plotted table", () => {
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
    const bars = [...host.querySelectorAll("rect.bof-viz-bar")];
    expect(bars).toHaveLength(2);
    expect(bars[0]!.getAttribute("style")).toContain("fill: rgb(255,0,0)");
    expect(bars[1]!.getAttribute("style")).toContain("fill: rgb(0,255,0)");
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

describe("fitToPane", () => {
  it("fills the pane's width, never narrower than the viz default", () => {
    expect(fitToPane(812.6)).toEqual({ width: 812, height: 360 });
    expect(fitToPane(300)).toEqual({ width: 480, height: 360 });
  });

  it("leaves the viz defaults when the pane has no layout yet", () => {
    expect(fitToPane(0)).toEqual({});
  });

  it("sizes a mounted chart to its pane unless the options set a size", () => {
    const sized = (options: Parameters<typeof createChartPane>[0]) => {
      const host = document.createElement("div");
      const pane = createChartPane(options);
      pane.mount(host, fakeCtx());
      // jsdom has no layout: give the pane's root (.bof-panes-root) a width.
      Object.defineProperty(host.firstElementChild, "clientWidth", { value: 900 });
      pane.update(tableInput);
      const width = host.querySelector("svg")!.getAttribute("width");
      pane.destroy();
      return width;
    };
    expect(sized({ chart: "bar" })).toBe("900");
    expect(sized({ chart: "bar", width: 640 })).toBe("640");
  });
});
