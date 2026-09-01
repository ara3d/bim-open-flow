import { describe, expect, it } from "vitest";
import type { NodeDescriptor, PortDescriptor } from "@bimopenflow/contracts";
import {
  chartPaneOptions,
  choosePanes,
  firstTableOutput,
  hasResults,
} from "../src/paneChoice.js";

const desc = (kind: string, outputs: PortDescriptor[]): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs,
  params: [],
  description: "",
});

describe("choosePanes", () => {
  it("offers only params + inspector without a descriptor", () => {
    expect(choosePanes(undefined)).toEqual(["params", "inspector"]);
  });

  it("offers only params + inspector for nodes without table outputs", () => {
    expect(choosePanes(desc("math.add", [{ name: "sum", type: "Number", optional: false }]))).toEqual([
      "params",
      "inspector",
    ]);
  });

  it("offers table + chart for table outputs", () => {
    expect(choosePanes(desc("table.select", [{ name: "out", type: "Table", optional: false }]))).toEqual([
      "table",
      "chart",
      "params",
      "inspector",
    ]);
  });

  it("puts verdict first for compliance kinds", () => {
    expect(choosePanes(desc("compliance.check", [{ name: "verdicts", type: "Table", optional: false }]))[0])
      .toBe("verdict");
    expect(choosePanes(desc("rollup.verdicts", [{ name: "out", type: "Table", optional: false }]))[0])
      .toBe("verdict");
  });

  it("puts chart first for chart.* kinds", () => {
    const out: PortDescriptor[] = [{ name: "out", type: "Table", optional: false }];
    expect(choosePanes(desc("chart.bar", out))).toEqual([
      "chart",
      "table",
      "params",
      "inspector",
    ]);
    expect(choosePanes(desc("chart.line", out))[0]).toBe("chart");
  });

  it("keeps table first for view.table and does not treat it as view3d", () => {
    const out: PortDescriptor[] = [{ name: "out", type: "Table", optional: false }];
    expect(choosePanes(desc("view.table", out))).toEqual([
      "table",
      "chart",
      "params",
      "inspector",
    ]);
  });

  it("puts view3d first for view3d kinds and instances outputs", () => {
    expect(choosePanes(desc("view3d.instances", [{ name: "out", type: "Table", optional: false }]))[0])
      .toBe("view3d");
    expect(choosePanes(desc("geometry.color", [{ name: "instances", type: "Table", optional: false }]))[0])
      .toBe("view3d");
  });
});

describe("chartPaneOptions", () => {
  it("maps chart.line params onto line chart options", () => {
    expect(
      chartPaneOptions("chart.line", {
        xColumn: "t",
        yColumns: " a, b ,,",
        title: "Trend",
      }),
    ).toEqual({
      chart: "line",
      xColumn: "t",
      seriesColumns: ["a", "b"],
      title: "Trend",
    });
  });

  it("maps chart.bar params onto bar chart options", () => {
    expect(
      chartPaneOptions("chart.bar", {
        labelColumn: "name",
        valueColumns: "area,count",
        title: "Areas",
      }),
    ).toEqual({
      chart: "bar",
      categoryColumn: "name",
      seriesColumns: ["area", "count"],
      title: "Areas",
    });
  });

  it("leaves unset params undefined so viz defaults apply", () => {
    expect(chartPaneOptions("chart.bar", {})).toEqual({
      chart: "bar",
      categoryColumn: undefined,
      seriesColumns: undefined,
      title: undefined,
    });
    expect(chartPaneOptions("chart.line", { yColumns: " , " })).toEqual({
      chart: "line",
      xColumn: undefined,
      seriesColumns: undefined,
      title: undefined,
    });
  });

  it("defaults any other kind to a plain bar chart", () => {
    expect(chartPaneOptions("table.select", { title: "x" })).toEqual({ chart: "bar" });
    expect(chartPaneOptions(undefined, {})).toEqual({ chart: "bar" });
  });
});

describe("hasResults", () => {
  it("is true only for an Ok node state", () => {
    expect(hasResults({ nodeId: "n", status: "Ok", warnings: [] })).toBe(true);
    for (const status of ["Unready", "EffectPending", "Unavailable", "Error"] as const)
      expect(hasResults({ nodeId: "n", status, warnings: [] })).toBe(false);
    expect(hasResults(undefined)).toBe(false);
  });
});

describe("firstTableOutput", () => {
  it("finds the first Table port", () => {
    const d = desc("x", [
      { name: "count", type: "Integer", optional: false },
      { name: "rows", type: "Table", optional: false },
    ]);
    expect(firstTableOutput(d)?.name).toBe("rows");
    expect(firstTableOutput(undefined)).toBeUndefined();
  });
});
