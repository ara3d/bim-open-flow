import { describe, expect, it } from "vitest";
import type { GraphDocument } from "@bimopenflow/state";
import { dataflowOrder, feeders } from "../src/graphOrder.js";

type Layout = Record<string, { x: number; y: number }>;

const doc = (ids: string[], edges: [string, string][], layout: Layout = {}): GraphDocument => ({
  formatVersion: "0.1.0",
  structure: {
    nodes: ids.map((id) => ({ id, kind: "k", version: 1 })),
    edges: edges.map(([from, to]) => ({ from, to })),
  },
  values: {},
  layout,
});

// The plan's worked example: doors -> join <- storeys, join -> answer.
const example = doc(
  ["answer", "join", "storeys", "doors"],
  [
    ["storeys.table", "join.right"],
    ["doors.table", "join.left"],
    ["join.table", "answer.input"],
  ],
  {
    doors: { x: 0, y: 0 },
    storeys: { x: 0, y: 200 },
    join: { x: 300, y: 100 },
    answer: { x: 600, y: 100 },
  },
);

describe("dataflowOrder", () => {
  it("orders the worked example by depth, then y", () => {
    expect(dataflowOrder(example)).toEqual(["doors", "storeys", "join", "answer"]);
  });

  it("breaks ties by x, then id", () => {
    const d = doc(["c", "b", "a"], [], { a: { x: 5, y: 0 }, b: { x: 1, y: 0 }, c: { x: 1, y: 0 } });
    expect(dataflowOrder(d)).toEqual(["b", "c", "a"]);
  });

  it("puts a node with no layout after laid-out nodes of its depth", () => {
    const d = doc(["loose", "placed", "other"], [], { placed: { x: 0, y: 900 }, other: { x: 0, y: 10 } });
    expect(dataflowOrder(d)).toEqual(["other", "placed", "loose"]);
  });

  it("lists every node of a cycle once, breaking it at the first node in the document", () => {
    const d = doc(["a", "b", "c"], [["a.o", "b.i"], ["b.o", "c.i"], ["c.o", "a.i"]]);
    expect(dataflowOrder(d)).toEqual(["a", "b", "c"]);
  });

  it("ignores edges naming a node that is not in the document", () => {
    const d = doc(["a"], [["ghost.o", "a.i"]]);
    expect(dataflowOrder(d)).toEqual(["a"]);
  });
});

describe("feeders", () => {
  it("names a node fed twice by the same node once", () => {
    const d = doc(["a", "b"], [["a.o", "b.left"], ["a.o", "b.right"]]);
    expect(feeders(d, "b")).toEqual(["a"]);
  });

  it("names the join's two feeders in dataflow order, each once", () => {
    expect(feeders(example, "join")).toEqual(["doors", "storeys"]);
    expect(feeders(example, "answer")).toEqual(["join"]);
    expect(feeders(example, "doors")).toEqual([]);
  });
});
