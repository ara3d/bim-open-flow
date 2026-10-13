import { describe, expect, it } from "vitest";
import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import { chartPaneOptions, choosePanes, firstTableOutput } from "../src/paneChoice.js";

const withOutput = (kind: string, type: PortType): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "out", type, optional: false }],
  params: [],
  description: "",
});

describe("firstTableOutput", () => {
  it("finds a Table output", () => {
    expect(firstTableOutput(withOutput("table.sort", "Table"))?.name).toBe("out");
  });

  it("treats a Relation output as pageable rows", () => {
    expect(firstTableOutput(withOutput("rel.filter", "Relation"))?.name).toBe("out");
  });

  it("treats a Database output as its table list", () => {
    expect(firstTableOutput(withOutput("duck.source", "Database"))?.name).toBe("out");
  });

  it("ignores scalar outputs", () => {
    expect(firstTableOutput(withOutput("test.const", "Integer"))).toBeUndefined();
  });
});

describe("choosePanes", () => {
  it("offers the table pane first for a relation node", () => {
    expect(choosePanes(withOutput("rel.join", "Relation"))).toEqual(["table", "chart", "inspector"]);
  });
});

describe("chartPaneOptions", () => {
  it("maps chart.bar params onto the pane's bar options, dropping empty and unknown choices", () => {
    expect(chartPaneOptions("chart.bar", {
      labelColumn: "Storey", valueColumns: "Doors, Windows", title: "Openings", layout: "stacked", colorBy: "series",
    })).toEqual({
      chart: "bar", categoryColumn: "Storey", seriesColumns: ["Doors", "Windows"], title: "Openings",
      layout: "stacked", colorBy: "series",
    });
    expect(chartPaneOptions("chart.bar", { layout: "radial", colorBy: "" })).toEqual({ chart: "bar" });
  });

  it("maps chart.line params and falls back to a plain bar for other kinds", () => {
    expect(chartPaneOptions("chart.line", { xColumn: "Week", yColumns: "Planned,Installed" }))
      .toEqual({ chart: "line", xColumn: "Week", seriesColumns: ["Planned", "Installed"] });
    expect(chartPaneOptions("table.sort", {})).toEqual({ chart: "bar" });
  });
});
