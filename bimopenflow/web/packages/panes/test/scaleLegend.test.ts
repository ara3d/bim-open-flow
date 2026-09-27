import { describe, expect, it } from "vitest";
import {
  entriesLegendView,
  parseScaleLegend,
  renderLegendView,
  scaleLegendView,
  type ScaleLegend,
} from "../src/scaleLegend";
import { makeSlice } from "./helpers";

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

const numericRows: unknown[][] = [
  ["meshVolume", "manual", "stop", "0", 0, 0.267, 0.005, 0.329, null],
  ["meshVolume", "manual", "stop", "0.25", 0.25, 0.283, 0.141, 0.458, null],
  ["meshVolume", "manual", "stop", "0.5", 0.5, 0.254, 0.265, 0.53, null],
  ["meshVolume", "manual", "stop", "0.75", 0.75, 0.207, 0.372, 0.553, null],
  ["meshVolume", "manual", "stop", "1", 1, 0.993, 0.906, 0.144, null],
  ["meshVolume", "manual", "above", "> 1", 1, 0.993, 0.906, 0.144, 2],
  ["meshVolume", "manual", "missing", "no value", null, 0.5, 0.5, 0.5, 1],
];

const categoricalRows: unknown[][] = [
  ["category", "categorical", "category", "Door", null, 0.122, 0.467, 0.706, 1],
  ["category", "categorical", "category", "Wall", null, 1, 0.498, 0.055, 2],
  ["category", "categorical", "missing", "no value", null, 0.5, 0.5, 0.5, 1],
];

describe("parseScaleLegend", () => {
  it("parses the numeric worked example", () => {
    const legend = parseScaleLegend(legendSlice(numericRows));
    expect(legend).not.toBeNull();
    expect(legend!.column).toBe("meshVolume");
    expect(legend!.domain).toBe("manual");
    expect(legend!.rows).toHaveLength(7);
    expect(legend!.rows.filter((r) => r.role === "stop")).toHaveLength(5);
    const above = legend!.rows.find((r) => r.role === "above")!;
    expect(above.label).toBe("> 1");
    expect(above.count).toBe(2);
    const missing = legend!.rows.find((r) => r.role === "missing")!;
    expect(missing.label).toBe("no value");
    expect(missing.count).toBe(1);
  });

  it("parses the categorical example", () => {
    const legend = parseScaleLegend(legendSlice(categoricalRows));
    expect(legend).not.toBeNull();
    expect(legend!.domain).toBe("categorical");
    expect(legend!.rows.map((r) => r.label)).toEqual(["Door", "Wall", "no value"]);
    expect(legend!.rows.map((r) => r.count)).toEqual([1, 2, 1]);
  });

  it("is null for an empty slice or one without a legend column", () => {
    expect(parseScaleLegend(makeSlice(LEGEND_COLUMNS, []))).toBeNull();
    expect(
      parseScaleLegend(makeSlice([["column", "Text"], ["value", "Number"]], [["meshVolume", 1]])),
    ).toBeNull();
  });
});

describe("scaleLegendView", () => {
  const legend: ScaleLegend = parseScaleLegend(legendSlice(numericRows))!;
  const view = scaleLegendView(legend);

  it("captions with the column, domain, and stop bounds", () => {
    expect(view.caption).toBe("meshVolume · manual domain 0 – 1");
  });

  it("builds a 5-stop gradient labelled only at its ends", () => {
    expect(view.gradient).toHaveLength(5);
    expect(view.gradient[0]!.label).toBe("0");
    expect(view.gradient[4]!.label).toBe("1");
    expect(view.gradient.slice(1, 4).every((stop) => stop.label === "")).toBe(true);
  });

  it("chips the below/above/missing rows", () => {
    expect(view.chips.map((c) => c.text)).toEqual(["> 1 (2)", "no value (1)"]);
  });
});

describe("entriesLegendView", () => {
  it("keeps today's chip text", () => {
    const view = entriesLegendView(
      [{ name: "Door", color: [0.122, 0.467, 0.706], count: 12 }],
      3,
    );
    expect(view.chips).toEqual([{ text: "Door (12 objects)", color: [0.122, 0.467, 0.706] }]);
    expect(view.gradient).toEqual([]);
    expect(view.omitted).toBe(3);
  });
});

describe("renderLegendView", () => {
  it("renders the same DOM twice for the same view", () => {
    const legend = parseScaleLegend(legendSlice(numericRows))!;
    const view = scaleLegendView(legend);
    const el = document.createElement("div");
    renderLegendView(el, view);
    const first = el.innerHTML;
    renderLegendView(el, view);
    expect(el.innerHTML).toBe(first);
    expect(el.hidden).toBe(false);
    expect(el.querySelector(".bof-panes-legend-caption")?.textContent).toBe(
      "meshVolume · manual domain 0 – 1",
    );
    expect(el.querySelectorAll(".bof-panes-legend-chip")).toHaveLength(2);
  });

  it("hides the element for an empty view", () => {
    const el = document.createElement("div");
    renderLegendView(el, { gradient: [], chips: [], omitted: 0 });
    expect(el.hidden).toBe(true);
    expect(el.children).toHaveLength(0);
  });
});
