import { describe, expect, it } from "vitest";
import { createChartPane } from "../src/chartPane";
import { conformance, tableInput } from "./conformance";
import { fakeCtx, makeSlice } from "./helpers";

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
});
