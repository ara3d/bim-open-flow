import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { filterCatalog } from "../src/catalogFilter.js";
import { freshNodeId, freshUntitledId } from "../src/ids.js";

const desc = (kind: string, description = ""): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [],
  params: [],
  description,
});

const catalog = [
  desc("source.model", "Loads a BIM model"),
  desc("table.select", "Selects columns from a table"),
  desc("compliance.check", "Rule check producing verdicts"),
];

describe("filterCatalog", () => {
  it("matches everything on an empty query", () => {
    expect(filterCatalog(catalog, "")).toHaveLength(3);
    expect(filterCatalog(catalog, "   ")).toHaveLength(3);
  });

  it("matches kind substrings case-insensitively", () => {
    expect(filterCatalog(catalog, "SELECT").map((n) => n.kind)).toEqual(["table.select"]);
  });

  it("matches description text", () => {
    expect(filterCatalog(catalog, "verdicts").map((n) => n.kind)).toEqual(["compliance.check"]);
  });

  it("requires every term to match", () => {
    expect(filterCatalog(catalog, "table columns")).toHaveLength(1);
    expect(filterCatalog(catalog, "table verdicts")).toHaveLength(0);
  });
});

describe("freshNodeId", () => {
  it("derives the base from the kind's last segment", () => {
    expect(freshNodeId("source.model", [])).toBe("model1");
  });

  it("skips taken ids", () => {
    expect(freshNodeId("source.model", ["model1", "model2"])).toBe("model3");
  });

  it("never emits a dot", () => {
    expect(freshNodeId("a.b.c", []).includes(".")).toBe(false);
  });
});

describe("freshUntitledId", () => {
  it("starts at untitled-1", () => {
    expect(freshUntitledId([])).toBe("untitled-1");
  });

  it("skips taken names, ignoring gaps in other ids", () => {
    expect(freshUntitledId(["untitled-1", "untitled-2", "renamed"])).toBe("untitled-3");
    expect(freshUntitledId(["untitled-2"])).toBe("untitled-1");
  });
});
