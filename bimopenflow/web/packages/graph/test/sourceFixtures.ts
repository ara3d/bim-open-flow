// Shared catalog and graph for the TKT-138 source tests: a DuckDB source, a path
// source, a reader whose path takes a Database, and a reader whose path takes Text.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";

const base = { version: 1, capability: "Pure" as const, description: "", inputs: [] };

export const duckSource: NodeDescriptor = {
  ...base, kind: "duck.source",
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: ".duckdb" } }],
};
export const valuePath: NodeDescriptor = {
  ...base, kind: "value.path",
  outputs: [{ name: "path", type: "Text", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: "" } }],
};
export const duckQuery: NodeDescriptor = {
  ...base, kind: "duck.query",
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [
    { name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: ".duckdb" }, bindable: "Database" },
    { name: "sql", kind: "Text", default: "" },
  ],
};
export const modelReader: NodeDescriptor = {
  ...base, kind: "bim.read",
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: ".ifc" }, bindable: "Text" }],
};
/** A reader that cannot take a source: its path has no socket. */
export const plainReader: NodeDescriptor = {
  ...base, kind: "bim.plain",
  outputs: [{ name: "table", type: "Table", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", suggest: { kind: "FilesUnderRoots", source: ".ifc" } }],
};

export const catalog = new Map([duckSource, valuePath, duckQuery, modelReader, plainReader].map((d) => [d.kind, d]));

/** Four duck.query nodes: q1 and q2 hold city.duckdb, q3 another file, q4 city.duckdb with backslashes. */
export function queryGraph() {
  const store = createStore();
  const paths: Record<string, string> = {
    q1: "C:/m/city.duckdb", q2: "C:/m/city.duckdb", q3: "C:/m/other.duckdb", q4: "C:\\m\\city.duckdb",
  };
  for (const [id, path] of Object.entries(paths)) {
    store.dispatch({ type: "addNode", id, kind: "duck.query", version: 1 });
    store.dispatch({ type: "setParam", nodeId: id, name: "path", value: path });
    store.dispatch({ type: "setLayout", nodeId: id, layout: { x: 300, y: Number(id.slice(1)) * 300 } });
  }
  return store;
}
