import { beforeEach, describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { filterCatalog, groupCatalog, loadExpandedPacks, saveExpandedPacks } from "../src/catalogFilter.js";
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

describe("groupCatalog", () => {
  it("collapses every pack when nothing is expanded, keeping the total count", () => {
    const groups = groupCatalog(catalog, "", new Set());
    expect(groups.map((g) => [g.pack, g.count, g.open])).toEqual([
      ["compliance", 1, false],
      ["source", 1, false],
      ["table", 1, false],
    ]);
  });

  it("opens exactly the packs named in expanded", () => {
    const groups = groupCatalog(catalog, "", new Set(["table"]));
    const table = groups.find((g) => g.pack === "table")!;
    const source = groups.find((g) => g.pack === "source")!;
    expect(table.open).toBe(true);
    expect(table.nodes.map((n) => n.kind)).toEqual(["table.select"]);
    expect(source.open).toBe(false);
  });

  it("with a query, opens only packs with a match and lists only matching nodes", () => {
    const groups = groupCatalog(catalog, "verdicts", new Set());
    expect(groups.map((g) => g.pack)).toEqual(["compliance"]);
    expect(groups[0]!.open).toBe(true);
    expect(groups[0]!.nodes.map((n) => n.kind)).toEqual(["compliance.check"]);
  });

  it("leaves the expanded set untouched while filtering, so clearing restores it", () => {
    const expanded = new Set(["source"]);
    groupCatalog(catalog, "verdicts", expanded);
    expect(expanded).toEqual(new Set(["source"]));
    const restored = groupCatalog(catalog, "", expanded);
    expect(restored.find((g) => g.pack === "source")!.open).toBe(true);
    expect(restored.find((g) => g.pack === "compliance")!.open).toBe(false);
  });
});

describe("expanded-pack prefs", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("round-trips through prefs", () => {
    saveExpandedPacks(new Set(["table", "chart"]));
    expect(loadExpandedPacks()).toEqual(new Set(["chart", "table"]));
  });

  it("defaults to empty when nothing is stored or the value is malformed", () => {
    expect(loadExpandedPacks()).toEqual(new Set());
    localStorage.setItem("bof-app-catalog-expanded", "not json");
    expect(loadExpandedPacks()).toEqual(new Set());
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
