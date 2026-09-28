import { describe, expect, it } from "vitest";
import type { NodeState } from "@bimopenflow/contracts";
import { nodeBadge, type BadgeGraph } from "../src/nodeBadge.js";

// A seeded graph exercising the three eval states TKT-10 must cover:
//   load  --ok-->      filter  --error-->  chart      (an Error node, its
//     own message shown, no upstream cause: the fault is local)
//   effect --pending--> report   (EffectPending: no cause, "Run to see results")
//   source (Unready, unwired) --> derive (Unready, cascades from source)
function graph(nodes: Record<string, NodeState>, edges: [string, string][]): BadgeGraph {
  return {
    edges: edges.map(([from, to]) => ({ from, to })),
    evalState: nodes,
  };
}

const state = (nodeId: string, status: NodeState["status"], error?: string): NodeState => ({
  nodeId,
  status,
  warnings: [],
  ...(error !== undefined ? { error } : {}),
});

describe("nodeBadge", () => {
  it("returns undefined before any eval update reaches the node", () => {
    const g = graph({}, []);
    expect(nodeBadge(g, "unknown")).toBeUndefined();
  });

  it("Ok reads as Ok with no upstream cause", () => {
    const g = graph({ load: state("load", "Ok") }, []);
    expect(nodeBadge(g, "load")).toEqual({ status: "Ok", text: "Ok" });
  });

  it("EffectPending reads 'Run to see results', not an error", () => {
    const g = graph({ effect: state("effect", "EffectPending") }, []);
    expect(nodeBadge(g, "effect")).toEqual({ status: "EffectPending", text: "Run to see results" });
  });

  it("Error with no upstream fault shows the host's own message, unset params read as local (no cause)", () => {
    const g = graph(
      {
        load: state("load", "Ok"),
        filter: state("filter", "Error", "bad expression"),
      },
      [["load.out", "filter.in"]],
    );
    expect(nodeBadge(g, "filter")).toEqual({
      status: "Error",
      text: "bad expression",
    });
  });

  it("Unready with an unset required parameter (no upstream fault) reads as needs-setup, not an error", () => {
    const g = graph({ derive: state("derive", "Unready") }, []);
    expect(nodeBadge(g, "derive")).toEqual({ status: "Unready", text: "Needs setup" });
  });

  it("Unready cascading from an upstream node names the upstream node responsible", () => {
    const g = graph(
      {
        source: state("source", "Unready"),
        derive: state("derive", "Unready"),
      },
      [["source.out", "derive.in"]],
    );
    expect(nodeBadge(g, "derive")).toEqual({
      status: "Unready",
      text: "Waiting on source",
      causeNodeId: "source",
    });
  });

  it("Unavailable always names the blocking upstream node, walking past intermediate Unready nodes", () => {
    const g = graph(
      {
        effect: state("effect", "EffectPending"),
        mid: state("mid", "Unavailable"),
        sink: state("sink", "Unavailable"),
      },
      [
        ["effect.out", "mid.in"],
        ["mid.out", "sink.in"],
      ],
    );
    expect(nodeBadge(g, "sink")).toEqual({
      status: "Unavailable",
      text: "Blocked by effect",
      causeNodeId: "effect",
    });
  });

  it("does not loop forever on a cycle (the document model otherwise disallows one)", () => {
    const g = graph(
      {
        a: state("a", "Unready"),
        b: state("b", "Unready"),
      },
      [
        ["a.out", "b.in"],
        ["b.out", "a.in"],
      ],
    );
    // A visited guard stops the walk; which node it settles on is incidental
    // to edge order in a graph that should never occur in practice.
    expect(nodeBadge(g, "a")).toEqual({ status: "Unready", text: "Waiting on b", causeNodeId: "b" });
  });
});
