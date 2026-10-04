import { describe, expect, it } from "vitest";
import type { NodeState } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { graphProblems, problemsSummary, type Problem } from "../src/graphProblems.js";

const st = (nodeId: string, status: NodeState["status"], error?: string): NodeState => ({
  nodeId,
  status,
  warnings: [],
  ...(error !== undefined ? { error } : {}),
});

function stateOf(
  nodes: string[],
  edges: [string, string][],
  evalState: Record<string, NodeState>,
): State {
  return {
    document: {
      formatVersion: "0.1.0",
      structure: {
        nodes: nodes.map((id) => ({ id, kind: "table.filter" })),
        edges: edges.map(([from, to]) => ({ from, to })),
      },
      values: {},
      layout: {},
    },
    selection: [],
    evalState,
    dirty: false,
    undoStack: [],
    redoStack: [],
  } as unknown as State;
}

describe("graphProblems", () => {
  it("lists a root Error first, with downstream nodes naming it as cause", () => {
    const s = stateOf(
      ["c", "b", "a"],
      [["a.out", "b.in"], ["b.out", "c.in"]],
      { a: st("a", "Error", "boom"), b: st("b", "Unavailable"), c: st("c", "Unavailable") },
    );
    const p = graphProblems(s);
    expect(p.map((x) => [x.nodeId, x.depth])).toEqual([["a", 0], ["b", 1], ["c", 2]]);
    expect(p[0]!.status).toBe("Error");
    expect(p[0]!.text).toBe("boom");
    expect(p[1]!.causeNodeId).toBe("a");
    expect(p[2]!.causeNodeId).toBe("a");
  });

  it("leaves out Ok nodes and nodes without state", () => {
    const s = stateOf(["a", "b", "c"], [], { a: st("a", "Ok"), b: st("b", "Unready") });
    expect(graphProblems(s).map((x) => x.nodeId)).toEqual(["b"]);
  });

  it("orders by status within one depth, then id", () => {
    const s = stateOf(["e", "d", "c", "b", "a"], [], {
      a: st("a", "Unready"),
      b: st("b", "Unavailable"),
      c: st("c", "EffectPending"),
      d: st("d", "Error", "x"),
      e: st("e", "Unready"),
    });
    expect(graphProblems(s).map((x) => x.nodeId)).toEqual(["d", "c", "b", "a", "e"]);
  });

  it("uses the longest upstream chain as depth", () => {
    const s = stateOf(
      ["a", "b", "c"],
      [["a.out", "b.in"], ["b.out", "c.in"], ["a.out", "c.in2"]],
      { a: st("a", "Unready"), b: st("b", "Unready"), c: st("c", "Unready") },
    );
    expect(graphProblems(s).find((x) => x.nodeId === "c")!.depth).toBe(2);
  });
});

describe("problemsSummary", () => {
  const p = (status: Problem["status"]): Problem => ({ nodeId: "n", title: "t", status, text: "", depth: 0 });
  it("is empty for none", () => expect(problemsSummary([])).toBe(""));
  it("reads one problem without an error count", () =>
    expect(problemsSummary([p("Unready")])).toBe("1 problem"));
  it("counts errors", () =>
    expect(problemsSummary([p("Error"), p("Unready"), p("Unavailable")])).toBe("3 problems · 1 error"));
});
