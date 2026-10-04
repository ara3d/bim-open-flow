import type { NodeDescriptor, PortDescriptor, PortType } from "@bimopenflow/contracts";
import { describe, expect, it } from "vitest";
import { filterPalette } from "../src/paletteFilter.js";

const port = (name: string, type: PortType): PortDescriptor => ({ name, type, optional: false });

const desc = (kind: string, description: string, inputs: PortDescriptor[] = [], outputs: PortDescriptor[] = []): NodeDescriptor => ({
  kind, version: 1, capability: "Pure", inputs, outputs, params: [], description,
});

const catalog = [
  desc("table.sort", "Orders rows by a column", [port("table", "Table")], [port("table", "Table")]),
  desc("chart.bar", "Bar chart of a table sorted by value", [port("rows", "Table")], [port("chart", "Any")]),
  desc("source.model", "Opens a model", [], [port("model", "Relation")]),
  desc("text.join", "Joins text", [port("a", "Text"), port("b", "Text")], [port("text", "Text")]),
  desc("view.any", "Shows anything", [port("value", "Any")], []),
];

const kinds = (entries: { desc: NodeDescriptor }[]) => entries.map((e) => e.desc.kind);

describe("filterPalette", () => {
  it("lists every kind, sorted, for an empty query", () => {
    expect(kinds(filterPalette(catalog, ""))).toEqual(["chart.bar", "source.model", "table.sort", "text.join", "view.any"]);
  });

  it("matches kind and description case-insensitively, kind matches first", () => {
    expect(kinds(filterPalette(catalog, "SORT"))).toEqual(["table.sort", "chart.bar"]);
    expect(kinds(filterPalette(catalog, "opens"))).toEqual(["source.model"]);
    expect(filterPalette(catalog, "nothing here")).toEqual([]);
  });

  it("from an output keeps kinds with a compatible input and names it", () => {
    const entries = filterPalette(catalog, "", { dir: "out", type: "Table" });
    expect(entries.map((e) => [e.desc.kind, e.port])).toEqual([
      ["chart.bar", "rows"],
      ["table.sort", "table"],
      ["view.any", "value"],
    ]);
  });

  it("from an input keeps kinds with a compatible output and names it", () => {
    const entries = filterPalette(catalog, "", { dir: "in", type: "Text" });
    // chart.bar's output is Any; source.model's Relation and view.any (no outputs) never match.
    expect(entries.map((e) => [e.desc.kind, e.port])).toEqual([["chart.bar", "chart"], ["text.join", "text"]]);
  });

  it("an Any wire matches every kind with a port on the other side", () => {
    expect(kinds(filterPalette(catalog, "", { dir: "out", type: "Any" }))).toEqual(["chart.bar", "table.sort", "text.join", "view.any"]);
  });

  it("never matches a port of the same direction", () => {
    // source.model only has a Relation output; a wire from another output cannot take it.
    expect(filterPalette(catalog, "model", { dir: "out", type: "Relation" })).toEqual([]);
    expect(filterPalette(catalog, "model", { dir: "in", type: "Relation" }).map((e) => e.port)).toEqual(["model"]);
  });

  it("applies the query and the wire together", () => {
    expect(kinds(filterPalette(catalog, "sort", { dir: "out", type: "Table" }))).toEqual(["table.sort", "chart.bar"]);
  });
});
