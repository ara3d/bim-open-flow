import { describe, expect, it } from "vitest";
import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import { choosePanes, firstTableOutput } from "../src/paneChoice.js";

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

  it("ignores scalar outputs", () => {
    expect(firstTableOutput(withOutput("test.const", "Integer"))).toBeUndefined();
  });
});

describe("choosePanes", () => {
  it("offers the table pane first for a relation node", () => {
    expect(choosePanes(withOutput("rel.join", "Relation"))).toEqual(["table", "chart", "params", "inspector"]);
  });
});
