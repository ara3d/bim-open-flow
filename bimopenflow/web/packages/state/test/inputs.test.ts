// Graph inputs in the client document (TKT-204): declared in structure.inputs,
// kept through parse, serialize, undo, and node removal.

import { describe, expect, it } from "vitest";
import { parseDocument, serializeDocument } from "../src/document.js";
import { initialState, reduce, referencedInput } from "../src/reducer.js";
import type { Action } from "../src/actions.js";

const run = (actions: Action[]) => actions.reduce(reduce, initialState);
const model = { name: "model", kind: "FilePath", default: "a.duckdb" } as const;

describe("graph inputs", () => {
  it("are declared, sorted by name, and replaced by a second declaration", () => {
    const state = run([
      { type: "declareInput", input: { ...model, name: "zeta" } },
      { type: "declareInput", input: model },
      { type: "declareInput", input: { ...model, default: "b.duckdb" } },
    ]);
    expect(state.document.structure.inputs?.map((i) => [i.name, i.default])).toEqual([["model", "b.duckdb"], ["zeta", "a.duckdb"]]);
  });

  it("round-trip through serialize and parse, and are left out when none are declared", () => {
    const state = run([{ type: "declareInput", input: { ...model, description: "The building" } }]);
    const json = serializeDocument(state.document);
    expect(JSON.parse(json).structure.inputs).toEqual([{ default: "a.duckdb", description: "The building", kind: "FilePath", name: "model" }]);
    expect(parseDocument(json).structure.inputs).toEqual(state.document.structure.inputs);
    expect(JSON.parse(serializeDocument(initialState.document)).structure).not.toHaveProperty("inputs");
  });

  it("survive removing a node, and a declaration undoes in one step", () => {
    const state = run([
      { type: "addNode", id: "a", kind: "k.a", version: 1 },
      { type: "declareInput", input: model },
      { type: "removeNode", id: "a" },
    ]);
    expect(state.document.structure.inputs).toHaveLength(1);
    expect(reduce(state, { type: "undo" }).document.structure.nodes.map((n) => n.id)).toEqual(["a"]);
    const declared = run([{ type: "declareInput", input: model }]);
    expect(reduce(declared, { type: "undo" }).document.structure.inputs).toBeUndefined();
  });

  it("cannot be removed while a parameter refers to them, and can be once none does", () => {
    const used = run([
      { type: "addNode", id: "db", kind: "duck.source", version: 1 },
      { type: "declareInput", input: model },
      { type: "setParam", nodeId: "db", name: "path", value: "$model" },
    ]);
    expect(() => reduce(used, { type: "removeInput", name: "model" })).toThrow(/still used by db.path/);
    const freed = reduce(used, { type: "setParam", nodeId: "db", name: "path", value: "x" });
    expect(reduce(freed, { type: "removeInput", name: "model" }).document.structure.inputs).toBeUndefined();
    expect(() => reduce(freed, { type: "removeInput", name: "nope" })).toThrow(/No graph input/);
  });

  it("reject a name the engine would not accept", () => {
    expect(() => run([{ type: "declareInput", input: { ...model, name: "my model" } }])).toThrow(/Invalid input name/);
  });

  it("take the host's current values from an evaluation update", () => {
    const info = { ...model, description: "", value: "b.duckdb" };
    const state = reduce(initialState, { type: "applyServerState", update: { analysisId: "a", nodes: [], graphInputs: [info] } });
    expect(state.graphInputs).toEqual([info]);
    const later = reduce(state, { type: "applyServerState", update: { analysisId: "a", nodes: [] } });
    expect(later.graphInputs).toEqual([info]);
  });

  it("are referred to by a whole value of the form $name", () => {
    expect(["$model", "$$model", "$", "a$b", "$1x", "$model "].map(referencedInput)).toEqual(["model", null, null, null, null, null]);
  });
});
