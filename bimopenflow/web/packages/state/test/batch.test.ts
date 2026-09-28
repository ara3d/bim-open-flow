import { describe, expect, it } from "vitest";
import { initialState, reduce } from "../src/reducer.js";
import type { Action } from "../src/actions.js";

const run = (actions: Action[]) => actions.reduce(reduce, initialState);

describe("batch action", () => {
  it("applies every inner action in order and undoes them as one step", () => {
    const state = run([
      {
        type: "batch",
        actions: [
          { type: "addNode", id: "a", kind: "k.a", version: 1 },
          { type: "setLayout", nodeId: "a", layout: { x: 10, y: 20 } },
          { type: "addNode", id: "b", kind: "k.b", version: 1 },
          { type: "connect", from: "a.out", to: "b.in" },
          { type: "select", ids: ["b"] },
        ],
      },
    ]);
    expect(state.document.structure.nodes.map((n) => n.id)).toEqual(["a", "b"]);
    expect(state.document.structure.edges).toEqual([{ from: "a.out", to: "b.in" }]);
    expect(state.document.layout.a).toEqual({ x: 10, y: 20 });
    expect(state.selection).toEqual(["b"]);
    expect(state.undoStack).toHaveLength(1);
    expect(state.dirty).toBe(true);

    const undone = reduce(state, { type: "undo" });
    expect(undone.document.structure.nodes).toEqual([]);
    expect(undone.undoStack).toHaveLength(0);
    expect(undone.redoStack).toHaveLength(1);
  });

  it("pushes no undo entry when no inner action edits the document", () => {
    const state = run([
      { type: "addNode", id: "a", kind: "k.a", version: 1 },
      { type: "batch", actions: [{ type: "select", ids: ["a"] }, { type: "clearSelection" }] },
    ]);
    expect(state.undoStack).toHaveLength(1);
    expect(state.selection).toEqual([]);
  });

  it("flattens a nested batch into the outer step", () => {
    const state = run([
      {
        type: "batch",
        actions: [
          { type: "addNode", id: "a", kind: "k.a", version: 1 },
          { type: "batch", actions: [{ type: "addNode", id: "b", kind: "k.b", version: 1 }] },
        ],
      },
    ]);
    expect(state.document.structure.nodes).toHaveLength(2);
    expect(state.undoStack).toHaveLength(1);
  });

  it("rejects the whole batch when an inner action is invalid", () => {
    expect(() =>
      run([
        {
          type: "batch",
          actions: [
            { type: "addNode", id: "a", kind: "k.a", version: 1 },
            { type: "connect", from: "a.out", to: "missing" },
          ],
        },
      ]),
    ).toThrow(/Invalid edge endpoint/);
  });
});
