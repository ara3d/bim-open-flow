import { describe, expect, it } from "vitest";
import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import { defaultShownNode } from "../src/defaultShown.js";

const doc = (
  nodeKinds: Record<string, string>,
  edges: [string, string][] = [],
  layout: GraphDocument["layout"] = {},
): GraphDocument => ({
  formatVersion: "0.1.0",
  structure: {
    nodes: Object.entries(nodeKinds).map(([id, kind]) => ({ id, kind, version: 1 })),
    edges: edges.map(([from, to]) => ({ from, to })),
  },
  values: {},
  layout,
});

const relationDesc: NodeDescriptor = {
  kind: "source.relation",
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "out", type: "Relation", optional: false }],
  params: [],
  description: "",
};

const tableDesc: NodeDescriptor = {
  kind: "table.select",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Relation", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

const viewerDesc: NodeDescriptor = {
  kind: "view3d.scene",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "view", type: "Table", optional: false }],
  params: [],
  description: "",
};

const ok: NodeState = { nodeId: "", status: "Ok", warnings: [] };
const unready: NodeState = { nodeId: "", status: "Unready", warnings: [] };

const catalogOf = (entries: Record<string, NodeDescriptor>) => new Map(Object.entries(entries));

describe("defaultShownNode", () => {
  it("picks the end of a linear graph", () => {
    const d = doc(
      { source: "source.relation", mid: "table.select", sink: "table.select" },
      [["source.out", "mid.in"], ["mid.out", "sink.in"]],
    );
    const catalog = catalogOf({ "source.relation": relationDesc, "table.select": tableDesc });
    const evalState = { source: ok, mid: ok, sink: ok };
    expect(defaultShownNode(d, evalState, catalog, null)).toBe("sink");
  });

  it("prefers a viewer/sink terminal over a plain relation terminal", () => {
    // source feeds both a table terminal and a viewer terminal.
    const d = doc(
      { source: "source.relation", table: "table.select", viewer: "view3d.scene" },
      [["source.out", "table.in"], ["source.out", "viewer.in"]],
    );
    const catalog = catalogOf({
      "source.relation": relationDesc,
      "table.select": tableDesc,
      "view3d.scene": viewerDesc,
    });
    const evalState = { source: ok, table: ok, viewer: ok };
    expect(defaultShownNode(d, evalState, catalog, null)).toBe("viewer");
  });

  it("still picks the only terminal when it has no result yet", () => {
    const d = doc({ source: "source.relation", sink: "table.select" }, [["source.out", "sink.in"]]);
    const catalog = catalogOf({ "source.relation": relationDesc, "table.select": tableDesc });
    const evalState = { source: ok, sink: unready };
    expect(defaultShownNode(d, evalState, catalog, null)).toBe("sink");
  });

  it("returns null for an empty graph", () => {
    const d = doc({});
    expect(defaultShownNode(d, {}, new Map(), null)).toBeNull();
  });

  it("returns null when every node has an outgoing edge (a cycle, no terminal)", () => {
    const d = doc(
      { a: "table.select", b: "table.select" },
      [["a.out", "b.in"], ["b.out", "a.in"]],
    );
    const catalog = catalogOf({ "table.select": tableDesc });
    expect(defaultShownNode(d, { a: ok, b: ok }, catalog, null)).toBeNull();
  });

  it("keeps lastShown when it still exists, even if another terminal would rank higher", () => {
    const d = doc(
      { source: "source.relation", table: "table.select", viewer: "view3d.scene" },
      [["source.out", "table.in"], ["source.out", "viewer.in"]],
    );
    const catalog = catalogOf({
      "source.relation": relationDesc,
      "table.select": tableDesc,
      "view3d.scene": viewerDesc,
    });
    const evalState = { source: ok, table: ok, viewer: ok };
    expect(defaultShownNode(d, evalState, catalog, "table")).toBe("table");
  });

  it("falls back to the ranking when lastShown no longer exists", () => {
    const d = doc({ sink: "table.select" });
    const catalog = catalogOf({ "table.select": tableDesc });
    expect(defaultShownNode(d, { sink: ok }, catalog, "gone")).toBe("sink");
  });
});
