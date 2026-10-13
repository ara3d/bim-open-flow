// TKT-296: compact cards. A compact card keeps its ports and the parameter
// rows a wire or a graph input feeds, drops the other editors and the
// description, shows one summary line, and ignores a width saved for the full
// card; the page-wide density re-sizes every card.

import { afterEach, describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore, type Store } from "@bimopenflow/state";
import { defaultCardDensity, setCardDensity, summaryLine, SUMMARY_MAX_CHARS, type CardDensity } from "../src/cardDensity.js";
import { buildCanvasModel } from "../src/viewModel.js";

afterEach(() => setCardDensity(defaultCardDensity));

const source: NodeDescriptor = {
  kind: "duck.source", version: 1, capability: "Pure", description: "A database file.",
  inputs: [],
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "" }],
};
const query: NodeDescriptor = {
  kind: "duck.query", version: 1, capability: "Pure",
  description: "Runs one SQL query against a database. The result is a table.",
  inputs: [],
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "path", kind: "FilePath", default: "", bindable: "Database" },
    { name: "sql", kind: "Text", default: "" },
    { name: "limit", kind: "Int", default: "0" },
  ],
};
const catalog = new Map([[source.kind, source], [query.kind, query]]);

function graph(): Store {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
  store.dispatch({ type: "addNode", id: "q", kind: "duck.query", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "C:/m/duplex.duckdb" });
  store.dispatch({ type: "setParam", nodeId: "q", name: "sql", value: "SELECT *\n  FROM doors" });
  store.dispatch({ type: "connect", from: "db.source", to: "q.path" });
  store.dispatch({ type: "setLayout", nodeId: "q", layout: { x: 400, y: 0, w: 600 } });
  return store;
}

const nodeOf = (store: Store, id: string, density: CardDensity) =>
  buildCanvasModel(store.getState(), catalog, null, undefined, { density }).nodes.find((n) => n.id === id)!;

describe("summaryLine", () => {
  it("names the first parameter set, with whitespace collapsed", () => {
    expect(summaryLine(["path", "sql"], { sql: "SELECT *\n  FROM doors" })).toBe("sql = SELECT * FROM doors");
  });

  it("skips blank values and is empty when none is set", () => {
    expect(summaryLine(["path", "sql"], { path: "  ", sql: "" })).toBe("");
    expect(summaryLine([], { sql: "x" })).toBe("");
  });

  it("cuts a long value to SUMMARY_MAX_CHARS with an ellipsis", () => {
    const line = summaryLine(["sql"], { sql: "x".repeat(100) });
    expect(line).toHaveLength(SUMMARY_MAX_CHARS);
    expect(line.endsWith("…")).toBe(true);
  });
});

describe("a compact card", () => {
  const store = graph();

  it("keeps only the wired parameter row and shows the summary in place of the description", () => {
    const full = nodeOf(store, "q", "full");
    const compact = nodeOf(store, "q", "compact");
    expect(full.params.map((p) => p.name)).toEqual(["path", "sql", "limit"]);
    expect(compact.params.map((p) => p.name)).toEqual(["path"]);
    expect(compact.description).toBe("sql = SELECT * FROM doors");
    expect(compact.inputs).toEqual(full.inputs);
    expect(compact.outputs).toEqual(full.outputs);
  });

  it("is shorter and ignores the width saved for the full card", () => {
    const full = nodeOf(store, "q", "full");
    const compact = nodeOf(store, "q", "compact");
    expect(full.w).toBe(600);
    expect(compact.w).toBeLessThan(full.w);
    expect(compact.h).toBeLessThan(full.h);
  });

  it("keeps a parameter that holds a graph input's reference", () => {
    const withInput = graph();
    withInput.dispatch({ type: "declareInput", input: { name: "model", kind: "FilePath", default: "C:/m/a.duckdb" } });
    withInput.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "$model" });
    expect(nodeOf(withInput, "db", "compact").params.map((p) => p.name)).toEqual(["path"]);
  });

  it("follows the page's density when the caller names none", () => {
    setCardDensity("compact");
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes.find((n) => n.id === "q")!.h).toBe(nodeOf(store, "q", "compact").h);
  });
});
