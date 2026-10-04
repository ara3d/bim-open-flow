import { describe, expect, it } from "vitest";
import type { NodeDescriptor, NodeState, PortType, TableSlice } from "@bimopenflow/contracts";
import type { PaneInput } from "@bimopenflow/panes";
import { feedTable, genericPanes, paneRegistry, panesFor, type PaneFeedIo, type PaneRegistration } from "../src/paneRegistry";
import { choosePanes } from "../src/paneChoice";
import type { ShownNode } from "../src/shownNode";

const node = (kind: string, type: PortType = "Table", name = "out"): NodeDescriptor => ({
  kind, version: 1, capability: "Pure", inputs: [], outputs: [{ name, type, optional: false }], params: [], description: "",
});

const ok: NodeState = { nodeId: "n", status: "Ok" } as NodeState;
const slice: TableSlice = { columns: [{ name: "a", type: "Text" }], rows: [["x"]], totalRows: 1, skip: 0 };

function fakePane() {
  const inputs: PaneInput[] = [];
  return { inputs, pane: { mount() {}, update: (i: PaneInput) => inputs.push(i), onEvent() {}, destroy() {} } };
}

function io(over: Partial<PaneFeedIo> = {}): PaneFeedIo & { notes: string[] } {
  const notes: string[] = [];
  return {
    notes,
    ctx: { requestTable: async () => slice, resolveAsset: (u) => u },
    port: { name: "out", type: "Table", optional: false },
    current: () => true,
    note: (t) => notes.push(t),
    ...over,
  };
}

const shown = (over: Partial<ShownNode> = {}): ShownNode => ({ nodeId: "n", desc: node("table.sort"), values: {}, state: ok, ...over });

describe("panesFor", () => {
  const registry = paneRegistry(...genericPanes);

  it("ranks the generic panes as the conventions do, without the 3D pane", () => {
    expect(panesFor(node("table.sort"), registry)).toEqual(["table", "chart", "inspector"]);
    expect(panesFor(node("chart.bar"), registry)).toEqual(["chart", "table", "inspector"]);
    expect(panesFor(node("compliance.check"), registry)).toEqual(["verdict", "table", "chart", "inspector"]);
    expect(panesFor(node("view3d.color", "Table", "instances"), registry)).toEqual(["table", "chart", "inspector"]);
    expect(panesFor(undefined, registry)).toEqual(["inspector"]);
  });

  it("places a registered pane by its offer", () => {
    const extra: PaneRegistration = { ...genericPanes[0]!, kind: "first", label: "First", offer: () => 0 };
    expect(panesFor(node("table.sort"), paneRegistry(...genericPanes, extra))[0]).toBe("first");
  });

  it("agrees with the one-argument choosePanes on the generic kinds", () => {
    const full = choosePanes(node("view3d.color", "Table", "instances"));
    expect(full).toEqual(["view3d", "table", "chart", "inspector"]);
  });
});

describe("paneRegistry", () => {
  it("refuses a kind registered twice", () => {
    expect(() => paneRegistry(genericPanes[0]!, genericPanes[0]!)).toThrow(/registered twice/);
  });
});

describe("feedTable", () => {
  it("pushes the first page once the node has a result", async () => {
    const { pane, inputs } = fakePane();
    await feedTable(pane, shown(), io());
    expect(inputs).toEqual([{ kind: "table", data: slice }]);
  });

  it("waits while the node is pending or has no result", async () => {
    const { pane, inputs } = fakePane();
    await feedTable(pane, shown({ pending: true }), io());
    await feedTable(pane, shown({ state: undefined }), io());
    expect(inputs).toEqual([]);
  });

  it("notes a failed read instead of throwing, and drops a stale one", async () => {
    const { pane, inputs } = fakePane();
    const failing = io({ ctx: { requestTable: async () => { throw new Error("gone"); }, resolveAsset: (u) => u } });
    await feedTable(pane, shown(), failing);
    expect(failing.notes).toEqual(["No rows to show: gone"]);
    const stale = io({ current: () => false });
    await feedTable(pane, shown(), stale);
    expect(inputs).toEqual([]);
  });
});
