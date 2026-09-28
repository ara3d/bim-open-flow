import type { NodeDescriptor, PortDescriptor, PortType } from "@bimopenflow/contracts";
import { initialState, reduce, type Action, type State } from "@bimopenflow/state";
import { describe, expect, it } from "vitest";
import { addNodeActions } from "../src/addNodePlan.js";
import { parseAnchorId } from "@bimopenflow/graph";
import { NODE_HEADER, NODE_WIDTH, PORT_SPACING, WIDE_NODE_WIDTH } from "@bimopenflow/graph";

const port = (name: string, type: PortType): PortDescriptor => ({ name, type, optional: false });

const sort: NodeDescriptor = {
  kind: "table.sort", version: 2, capability: "Pure",
  inputs: [port("left", "Table"), port("table", "Table")], outputs: [port("first", "Table"), port("sorted", "Table")],
  params: [], description: "Orders rows",
};
const filter: NodeDescriptor = {
  ...sort, kind: "table.filter",
  params: [{ name: "where", kind: "Text", default: "" }],
};

/** A document with node sort1 already in it, so the fresh id is sort2. */
function stateWithSort(): State {
  return [
    { type: "addNode", id: "sort1", kind: "table.sort", version: 2 },
    { type: "setLayout", nodeId: "sort1", layout: { x: 0, y: 0 } },
  ].reduce<State>((s, a) => reduce(s, a as Action), initialState);
}

describe("addNodeActions", () => {
  it("adds, places, and selects a fresh node at the point", () => {
    const actions = addNodeActions(stateWithSort(), sort, { x: 40, y: 70 });
    expect(actions).toEqual([
      { type: "addNode", id: "sort2", kind: "table.sort", version: 2 },
      { type: "setLayout", nodeId: "sort2", layout: { x: 40, y: 70 } },
      { type: "select", ids: ["sort2"] },
    ]);
  });

  it("from an output anchor connects the new node's input, left edge at the drop point", () => {
    const actions = addNodeActions(stateWithSort(), sort, { x: 300, y: 200 },
      { from: parseAnchorId("out:sort1.sorted"), port: "table" });
    expect(actions.map((a) => a.type)).toEqual(["addNode", "setLayout", "select", "connect"]);
    expect(actions[1]).toEqual({ type: "setLayout", nodeId: "sort2",
      layout: { x: 300, y: 200 - NODE_HEADER - 1.5 * PORT_SPACING } });
    expect(actions[3]).toEqual({ type: "connect", from: "sort1.sorted", to: "sort2.table" });
  });

  it("from an input anchor connects the new node's output, right edge at the drop point", () => {
    const narrow = addNodeActions(stateWithSort(), sort, { x: 300, y: 200 },
      { from: parseAnchorId("in:sort1.table"), port: "first" });
    expect(narrow[1]).toEqual({ type: "setLayout", nodeId: "sort2",
      layout: { x: 300 - NODE_WIDTH, y: 200 - NODE_HEADER - 0.5 * PORT_SPACING } });
    expect(narrow[3]).toEqual({ type: "connect", from: "sort2.first", to: "sort1.table" });
    // A kind with inline parameters is drawn wider.
    const wide = addNodeActions(stateWithSort(), filter, { x: 300, y: 200 },
      { from: parseAnchorId("in:sort1.table"), port: "first" });
    expect(wide[1]).toMatchObject({ layout: { x: 300 - WIDE_NODE_WIDTH } });
  });

  it("dispatched as a batch, the whole add is one undo step", () => {
    const before = stateWithSort();
    const actions = addNodeActions(before, sort, { x: 300, y: 200 },
      { from: parseAnchorId("out:sort1.sorted"), port: "table" });
    const after = reduce(before, { type: "batch", actions });
    expect(after.undoStack).toHaveLength(before.undoStack.length + 1);
    expect(after.selection).toEqual(["sort2"]);
    expect(after.document.structure.edges).toEqual([{ from: "sort1.sorted", to: "sort2.table" }]);
    const undone = reduce(after, { type: "undo" });
    expect(undone.document.structure.nodes.map((n) => n.id)).toEqual(["sort1"]);
    expect(undone.document.structure.edges).toEqual([]);
  });
});
