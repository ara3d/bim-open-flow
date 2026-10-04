import { describe, expect, it } from "vitest";
import { LEGEND_MAX_ENTRIES, legendColumn, legendFromSlice } from "../src/instanceLegend";
import { makeSlice } from "./helpers";

const colored = (rows: unknown[][]) =>
  makeSlice(
    [["entityId", "Integer"], ["category", "Text"], ["r", "Number"], ["g", "Number"], ["b", "Number"], ["a", "Number"]],
    rows,
  );

describe("legendColumn", () => {
  it("prefers verdict over category and ignores other columns", () => {
    expect(legendColumn([{ name: "category", type: "Text" }, { name: "verdict", type: "Text" }])).toBe(1);
    expect(legendColumn([{ name: "entityId", type: "Integer" }, { name: "category", type: "Text" }])).toBe(1);
    expect(legendColumn([{ name: "entityId", type: "Integer" }])).toBe(-1);
  });
});

describe("legendFromSlice", () => {
  it("lists distinct values with the first row's colour, most frequent first, ties by name", () => {
    const legend = legendFromSlice(colored([
      [1, "IfcWall", 1, 0, 0, 1],
      [2, "IfcSlab", 0, 1, 0, 1],
      [3, "IfcWall", 1, 0, 0, 1],
      [4, "IfcDoor", 0, 0, 1, 1],
    ]));
    expect(legend.omitted).toBe(0);
    expect(legend.entries).toEqual([
      { name: "IfcWall", color: [1, 0, 0], count: 2 },
      { name: "IfcDoor", color: [0, 0, 1], count: 1 },
      { name: "IfcSlab", color: [0, 1, 0], count: 1 },
    ]);
  });

  it("labels null and empty values as blank", () => {
    const legend = legendFromSlice(colored([[1, null, 0.5, 0.5, 0.5, 1], [2, "", 0.5, 0.5, 0.5, 1]]));
    expect(legend.entries).toEqual([{ name: "(blank)", color: [0.5, 0.5, 0.5], count: 2 }]);
  });

  it("caps the entries and counts the rest as omitted", () => {
    const rows = Array.from({ length: LEGEND_MAX_ENTRIES + 3 }, (_, i) => [i, `Cat${i}`, 0, 0, 0, 1]);
    const legend = legendFromSlice(colored(rows));
    expect(legend.entries.length).toBe(LEGEND_MAX_ENTRIES);
    expect(legend.omitted).toBe(3);
    expect(legendFromSlice(colored(rows), 5).entries.length).toBe(5);
  });

  it("is empty without a legend column or without colours", () => {
    expect(legendFromSlice(makeSlice([["entityId", "Integer"], ["r", "Number"], ["g", "Number"], ["b", "Number"]], [[1, 1, 1, 1]])).entries).toEqual([]);
    expect(legendFromSlice(makeSlice([["entityId", "Integer"], ["category", "Text"]], [[1, "IfcWall"]])).entries).toEqual([]);
  });
});
