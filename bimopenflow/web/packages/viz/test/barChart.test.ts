import { describe, expect, it } from "vitest";
import type { TableData } from "@bimopenflow/contracts";
import { BarChart } from "../src/barChart";

const data: TableData = {
  columns: [
    { name: "Category", type: "Text" },
    { name: "Value", type: "Number" },
  ],
  rows: [
    ["A", 5],
    ["B", -3],
    ["C", 2],
  ],
};

const mountChart = (d: TableData = data, options = {}) => {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const handle = BarChart.mount(container, d, options);
  return { container, handle };
};

describe("BarChart", () => {
  it("renders one rect per row", () => {
    const { container } = mountChart();
    expect(container.querySelectorAll("rect.bof-viz-bar")).toHaveLength(3);
  });

  it("draws negative bars below the zero line with the negative class", () => {
    const { container } = mountChart();
    const bars = [...container.querySelectorAll("rect.bof-viz-bar")];
    const positive = bars[0];
    const negative = bars[1];
    expect(negative.classList.contains("bof-viz-bar--neg")).toBe(true);
    expect(positive.classList.contains("bof-viz-bar--neg")).toBe(false);
    // positive bar starts above where the negative bar starts (zero line)
    expect(Number(positive.getAttribute("y"))).toBeLessThan(
      Number(negative.getAttribute("y")),
    );
    // positive bar's bottom edge is the negative bar's top edge (both at zero)
    const posBottom =
      Number(positive.getAttribute("y")) +
      Number(positive.getAttribute("height"));
    expect(posBottom).toBeCloseTo(Number(negative.getAttribute("y")), 6);
  });

  it("renders category and value labels", () => {
    const { container } = mountChart();
    const axisLabels = [
      ...container.querySelectorAll("text.bof-viz-axis-label"),
    ].map((t) => t.textContent);
    expect(axisLabels).toEqual(["A", "B", "C"]);
    const valueLabels = [
      ...container.querySelectorAll("text.bof-viz-value-label"),
    ].map((t) => t.textContent);
    expect(valueLabels).toEqual(["5", "-3", "2"]);
  });

  it("renders y-axis tick labels", () => {
    const { container } = mountChart();
    expect(
      container.querySelectorAll("text.bof-viz-tick-label").length,
    ).toBeGreaterThan(1);
  });

  it("update() re-renders", () => {
    const { container, handle } = mountChart();
    handle.update({ ...data, rows: [["Z", 7]] });
    const bars = container.querySelectorAll("rect.bof-viz-bar");
    expect(bars).toHaveLength(1);
    expect(bars[0].getAttribute("data-value")).toBe("7");
  });

  it("destroy() removes everything it added", () => {
    const { container, handle } = mountChart();
    handle.destroy();
    expect(container.children).toHaveLength(0);
  });

  it("respects explicit column options", () => {
    const swapped: TableData = {
      columns: [
        { name: "x", type: "Number" },
        { name: "label", type: "Text" },
        { name: "y", type: "Number" },
      ],
      rows: [[1, "p", 10]],
    };
    const { container } = mountChart(swapped, {
      categoryColumn: "label",
      valueColumn: "y",
    });
    const bar = container.querySelector("rect.bof-viz-bar");
    expect(bar?.getAttribute("data-value")).toBe("10");
  });
});
