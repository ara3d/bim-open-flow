import type { NodeDescriptor, PortDescriptor, PortType } from "@bimopenflow/contracts";
import { initialState, reduce, type Action, type State } from "@bimopenflow/state";
import { describe, expect, it } from "vitest";
import { planNodes, planSetParams } from "../src/paneEdits.js";

const port = (name: string, type: PortType): PortDescriptor => ({ name, type, optional: false });
const desc = (kind: string, inputs: PortDescriptor[], outputs: PortDescriptor[]): NodeDescriptor => ({
  kind, version: 1, capability: "Pure", inputs, outputs, params: [], description: "",
});
const catalog = new Map([
  ["bim.selection", desc("bim.selection", [], [port("ids", "Table")])],
  ["table.join", desc("table.join", [port("left", "Table"), port("right", "Table")], [port("joined", "Table")])],
].map(([k, d]) => [k as string, d as NodeDescriptor]));

const apply = (state: State, a: Action) => reduce(state, a);

describe("planNodes", () => {
  it("plans two wired nodes as one batch with ids, layout, params, connect, and the last selected", () => {
    const plan = planNodes(initialState, catalog, [
      { kind: "bim.selection", params: { ids: "a,b" } },
      { kind: "table.join", inputs: { left: { node: 0, port: "ids" }, right: { nodeId: "other", port: "t" } } },
    ]);
    expect(plan.ids).toEqual(["selection1", "join1"]);
    expect(plan.action.type).toBe("batch");
    const inner = (plan.action as { actions: readonly Action[] }).actions;
    expect(inner.filter((a) => a.type === "addNode").map((a) => (a as { id: string }).id)).toEqual(plan.ids);
    const layouts = inner.filter((a) => a.type === "setLayout") as Extract<Action, { type: "setLayout" }>[];
    expect(layouts).toHaveLength(2);
    expect(layouts[0]!.layout).not.toEqual(layouts[1]!.layout);
    expect(inner).toContainEqual({ type: "setParam", nodeId: "selection1", name: "ids", value: "a,b" });
    expect(inner).toContainEqual({ type: "connect", from: "selection1.ids", to: "join1.left" });
    expect(inner).toContainEqual({ type: "connect", from: "other.t", to: "join1.right" });
    expect(inner.filter((a) => a.type === "select")).toEqual([{ type: "select", ids: ["join1"] }]);
    expect(inner[inner.length - 1]).toEqual({ type: "select", ids: ["join1"] });
  });

  it("rejects an unknown kind and a forward reference", () => {
    expect(() => planNodes(initialState, catalog, [{ kind: "nope" }])).toThrow("Unknown node kind 'nope'");
    expect(() => planNodes(initialState, catalog, [
      { kind: "table.join", inputs: { left: { node: 1, port: "ids" } } },
    ])).toThrow(/not planned before it/);
  });
});

describe("planSetParams", () => {
  const withNode = apply(initialState, { type: "addNode", id: "n1", kind: "bim.selection", version: 1 });

  it("sets each param in one batch", () => {
    expect(planSetParams(withNode, "n1", { a: "1", b: "2" })).toEqual({
      type: "batch",
      actions: [
        { type: "setParam", nodeId: "n1", name: "a", value: "1" },
        { type: "setParam", nodeId: "n1", name: "b", value: "2" },
      ],
    });
  });

  it("rejects a node that is not in the document", () => {
    expect(() => planSetParams(withNode, "gone", { a: "1" })).toThrow("No node 'gone'");
  });
});
