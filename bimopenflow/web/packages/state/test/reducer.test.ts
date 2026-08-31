import { describe, expect, it } from "vitest";
import type { EvalUpdate, NodeState } from "@bimopenflow/contracts";
import { initialState, reduce, type State } from "../src/reducer.js";
import type { Action } from "../src/actions.js";

function apply(state: State, ...actions: Action[]): State {
  return actions.reduce(reduce, state);
}

const twoNodes = apply(
  initialState,
  { type: "addNode", id: "a", kind: "source.model", version: 1 },
  { type: "addNode", id: "b", kind: "sink.table", version: 1 },
);

describe("addNode", () => {
  it("appends a node and marks dirty", () => {
    const s = apply(initialState, { type: "addNode", id: "a", kind: "source.model", version: 1 });
    expect(s.document.structure.nodes).toEqual([{ id: "a", kind: "source.model", version: 1 }]);
    expect(s.dirty).toBe(true);
    expect(s.undoStack).toHaveLength(1);
  });

  it("rejects duplicate and invalid ids", () => {
    expect(() => reduce(twoNodes, { type: "addNode", id: "a", kind: "x", version: 1 })).toThrow(/already exists/);
    expect(() => reduce(twoNodes, { type: "addNode", id: "a.b", kind: "x", version: 1 })).toThrow(/no dot/);
    expect(() => reduce(twoNodes, { type: "addNode", id: "", kind: "x", version: 1 })).toThrow(/non-empty/);
  });

  it("does not mutate the previous state", () => {
    const before = twoNodes.document.structure.nodes.length;
    reduce(twoNodes, { type: "addNode", id: "c", kind: "x", version: 1 });
    expect(twoNodes.document.structure.nodes).toHaveLength(before);
  });
});

describe("removeNode", () => {
  it("drops the node with its edges, values, and layout", () => {
    const s = apply(
      twoNodes,
      { type: "connect", from: "a.out", to: "b.in" },
      { type: "setParam", nodeId: "a", name: "path", value: "m.bos" },
      { type: "setLayout", nodeId: "a", layout: { x: 1, y: 2 } },
      { type: "removeNode", id: "a" },
    );
    expect(s.document.structure.nodes.map((n) => n.id)).toEqual(["b"]);
    expect(s.document.structure.edges).toEqual([]);
    expect(s.document.values).toEqual({});
    expect(s.document.layout).toEqual({});
  });

  it("throws for an unknown node", () => {
    expect(() => reduce(twoNodes, { type: "removeNode", id: "zz" })).toThrow(/No node/);
  });
});

describe("connect / disconnect", () => {
  it("adds an edge", () => {
    const s = reduce(twoNodes, { type: "connect", from: "a.out", to: "b.in" });
    expect(s.document.structure.edges).toEqual([{ from: "a.out", to: "b.in" }]);
  });

  it("replaces an existing edge into the same input port", () => {
    const s = apply(
      twoNodes,
      { type: "addNode", id: "c", kind: "source.model", version: 1 },
      { type: "connect", from: "a.out", to: "b.in" },
      { type: "connect", from: "c.out", to: "b.in" },
    );
    expect(s.document.structure.edges).toEqual([{ from: "c.out", to: "b.in" }]);
  });

  it("rejects malformed endpoints", () => {
    expect(() => reduce(twoNodes, { type: "connect", from: "aout", to: "b.in" })).toThrow(/endpoint/);
  });

  it("disconnect removes exactly the named edge and throws if absent", () => {
    const connected = reduce(twoNodes, { type: "connect", from: "a.out", to: "b.in" });
    const s = reduce(connected, { type: "disconnect", from: "a.out", to: "b.in" });
    expect(s.document.structure.edges).toEqual([]);
    expect(() => reduce(s, { type: "disconnect", from: "a.out", to: "b.in" })).toThrow(/No edge/);
  });
});

describe("setParam / setLayout", () => {
  it("sets a parameter value as a string", () => {
    const s = reduce(twoNodes, { type: "setParam", nodeId: "a", name: "path", value: "m.bos" });
    expect(s.document.values).toEqual({ a: { path: "m.bos" } });
  });

  it("overwrites an existing parameter and keeps siblings", () => {
    const s = apply(
      twoNodes,
      { type: "setParam", nodeId: "a", name: "path", value: "m.bos" },
      { type: "setParam", nodeId: "a", name: "mode", value: "full" },
      { type: "setParam", nodeId: "a", name: "path", value: "n.bos" },
    );
    expect(s.document.values).toEqual({ a: { path: "n.bos", mode: "full" } });
  });

  it("sets layout and throws for unknown nodes", () => {
    const s = reduce(twoNodes, { type: "setLayout", nodeId: "a", layout: { x: 5, y: 6, w: 7 } });
    expect(s.document.layout.a).toEqual({ x: 5, y: 6, w: 7 });
    expect(() => reduce(twoNodes, { type: "setParam", nodeId: "zz", name: "p", value: "v" })).toThrow(/No node/);
    expect(() => reduce(twoNodes, { type: "setLayout", nodeId: "zz", layout: { x: 0, y: 0 } })).toThrow(/No node/);
  });
});

describe("selection", () => {
  it("select sets and clearSelection empties, without touching dirty or undo", () => {
    const s = reduce(twoNodes, { type: "select", ids: ["a", "b"] });
    expect(s.selection).toEqual(["a", "b"]);
    expect(s.undoStack).toEqual(twoNodes.undoStack);
    const cleared = reduce(s, { type: "clearSelection" });
    expect(cleared.selection).toEqual([]);
  });
});

describe("undo / redo", () => {
  it("undo restores the previous document; redo reapplies", () => {
    const edited = reduce(twoNodes, { type: "connect", from: "a.out", to: "b.in" });
    const undone = reduce(edited, { type: "undo" });
    expect(undone.document.structure.edges).toEqual([]);
    const redone = reduce(undone, { type: "redo" });
    expect(redone.document.structure.edges).toEqual([{ from: "a.out", to: "b.in" }]);
  });

  it("is a no-op at the stack boundaries", () => {
    expect(reduce(initialState, { type: "undo" })).toBe(initialState);
    expect(reduce(initialState, { type: "redo" })).toBe(initialState);
  });

  it("a new edit clears the redo stack", () => {
    const undone = apply(twoNodes, { type: "connect", from: "a.out", to: "b.in" }, { type: "undo" });
    expect(undone.redoStack).toHaveLength(1);
    const s = reduce(undone, { type: "setParam", nodeId: "a", name: "p", value: "v" });
    expect(s.redoStack).toEqual([]);
  });

  it("selection is not part of undo history", () => {
    const s = apply(
      twoNodes,
      { type: "select", ids: ["a"] },
      { type: "connect", from: "a.out", to: "b.in" },
      { type: "select", ids: ["b"] },
      { type: "undo" },
    );
    expect(s.selection).toEqual(["b"]);
    expect(s.document.structure.edges).toEqual([]);
  });
});

describe("applyServerState", () => {
  const ok = (nodeId: string): NodeState => ({ nodeId, status: "Ok", warnings: [] });
  const err = (nodeId: string): NodeState => ({ nodeId, status: "Error", error: "boom", warnings: [] });

  it("merges reported nodes over existing eval state", () => {
    const first: EvalUpdate = { analysisId: "x", nodes: [ok("a"), ok("b")] };
    const second: EvalUpdate = { analysisId: "x", nodes: [err("b")] };
    const s = apply(twoNodes, { type: "applyServerState", update: first }, { type: "applyServerState", update: second });
    expect(s.evalState.a).toEqual(ok("a"));
    expect(s.evalState.b).toEqual(err("b"));
  });

  it("does not mark the document dirty or touch undo", () => {
    const s = reduce(initialState, { type: "applyServerState", update: { analysisId: "x", nodes: [ok("a")] } });
    expect(s.dirty).toBe(false);
    expect(s.undoStack).toEqual([]);
  });
});

describe("setDocument / markSaved", () => {
  const json = JSON.stringify({
    structure: { nodes: [{ id: "n", kind: "k", version: 1 }], edges: [] },
    values: {},
  });

  it("replaces the document and resets selection, history, eval state, and dirty", () => {
    const busy = apply(
      twoNodes,
      { type: "select", ids: ["a"] },
      { type: "applyServerState", update: { analysisId: "x", nodes: [{ nodeId: "a", status: "Ok", warnings: [] }] } },
    );
    const s = reduce(busy, { type: "setDocument", json });
    expect(s.document.structure.nodes.map((n) => n.id)).toEqual(["n"]);
    expect(s.selection).toEqual([]);
    expect(s.evalState).toEqual({});
    expect(s.undoStack).toEqual([]);
    expect(s.redoStack).toEqual([]);
    expect(s.dirty).toBe(false);
  });

  it("markSaved clears dirty only", () => {
    const s = reduce(twoNodes, { type: "markSaved" });
    expect(s.dirty).toBe(false);
    expect(s.document).toBe(twoNodes.document);
    expect(s.undoStack).toEqual(twoNodes.undoStack);
  });
});
