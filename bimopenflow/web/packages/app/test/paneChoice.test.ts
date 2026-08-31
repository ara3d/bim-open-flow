import { describe, expect, it } from "vitest";
import type { NodeDescriptor, PortDescriptor } from "@bimopenflow/contracts";
import { choosePanes, firstTableOutput } from "../src/paneChoice.js";

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
    expect(choosePanes(desc("math.add", [{ name: "sum", type: "Number" }]))).toEqual([
      "params",
      "inspector",
    ]);
  });

  it("offers table + chart for table outputs", () => {
    expect(choosePanes(desc("table.select", [{ name: "out", type: "Table" }]))).toEqual([
      "table",
      "chart",
      "params",
      "inspector",
    ]);
  });

  it("puts verdict first for compliance kinds", () => {
    expect(choosePanes(desc("compliance.check", [{ name: "verdicts", type: "Table" }]))[0])
      .toBe("verdict");
    expect(choosePanes(desc("rollup.verdicts", [{ name: "out", type: "Table" }]))[0])
      .toBe("verdict");
  });

  it("puts view3d first for view3d kinds and instances outputs", () => {
    expect(choosePanes(desc("view3d.instances", [{ name: "out", type: "Table" }]))[0])
      .toBe("view3d");
    expect(choosePanes(desc("geometry.color", [{ name: "instances", type: "Table" }]))[0])
      .toBe("view3d");
  });
});

describe("firstTableOutput", () => {
  it("finds the first Table port", () => {
    const d = desc("x", [
      { name: "count", type: "Integer" },
      { name: "rows", type: "Table" },
    ]);
    expect(firstTableOutput(d)?.name).toBe("rows");
    expect(firstTableOutput(undefined)).toBeUndefined();
  });
});
