import { describe, expect, it } from "vitest";
import type { TableData } from "@bimopenflow/contracts";
import { LineChart } from "../src/lineChart";

const data: TableData = {
  columns: [
    { name: "t", type: "Number" },
    { name: "a", type: "Number" },
    { name: "b", type: "Integer" },
  ],
  rows: [
    [0, 1, 10],
    [1, 2, 20],
    [2, 4, 15],
    [3, 3, 25],
  ],
};

const mountChart = (d: TableData = data, options = {}) => {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const handle = LineChart.mount(container, d, options);
  return { container, handle };
};

describe("LineChart", () => {
  it("renders one path per numeric series (excluding the x column)", () => {
    const { container } = mountChart(data, { xColumn: "t" });
    const paths = [...container.querySelectorAll("path.bof-viz-line")];
    expect(paths.map((p) => p.getAttribute("data-series"))).toEqual(["a", "b"]);
  });

  it("uses the row index when no x column is given", () => {
    const { container } = mountChart();
    // all three numeric columns become series
    expect(container.querySelectorAll("path.bof-viz-line")).toHaveLength(3);
  });

  it("path has one segment per row", () => {
    const { container } = mountChart(data, {
      xColumn: "t",
      seriesColumns: ["a"],
    });
    const d = container
      .querySelector("path.bof-viz-line")
      ?.getAttribute("d") as string;
    expect(d.match(/[ML]/g)).toHaveLength(4);
    expect(d.startsWith("M")).toBe(true);
  });

  it("breaks the path at null values", () => {
    const gappy: TableData = {
      columns: [{ name: "a", type: "Number" }],
      rows: [[1], [2], [null], [4], [5]],
    };
    const { container } = mountChart(gappy);
    const d = container
      .querySelector("path.bof-viz-line")
      ?.getAttribute("d") as string;
    expect(d.match(/M/g)).toHaveLength(2);
  });

  it("renders tick labels on both axes", () => {
    const { container } = mountChart(data, { xColumn: "t" });
    expect(
      container.querySelectorAll("text.bof-viz-tick-label").length,
    ).toBeGreaterThan(3);
  });

  it("update() re-renders", () => {
    const { container, handle } = mountChart(data, { xColumn: "t" });
    handle.update({
      columns: [
        { name: "t", type: "Number" },
        { name: "only", type: "Number" },
      ],
      rows: [
        [0, 1],
        [1, 2],
      ],
    });
    const paths = container.querySelectorAll("path.bof-viz-line");
    expect(paths).toHaveLength(1);
    expect(paths[0].getAttribute("data-series")).toBe("only");
  });

  it("destroy() removes everything it added", () => {
    const { container, handle } = mountChart();
    handle.destroy();
    expect(container.children).toHaveLength(0);
  });
});
