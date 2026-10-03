// The DuckDB studio's pane area (tableOnly): a table for every node, and a
// Chart tab ahead of it for chart.* nodes (TKT-20).
import { describe, expect, it } from "vitest";
import type { NodeDescriptor, NodeState, TableSlice } from "@bimopenflow/contracts";
import { createPaneArea } from "../src/paneArea.js";
import { studioPanes } from "@bimopenflow/client";

const desc = (kind: string): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [
    { name: "table", type: "Table", optional: false },
    { name: "legend", type: "Table", optional: false },
  ],
  params: [],
  description: "",
});

const okState: NodeState = { nodeId: "chart", status: "Ok", warnings: [] };

const slice: TableSlice = {
  columns: [
    { name: "Storey", type: "Text" },
    { name: "Rooms", type: "Integer" },
  ],
  rows: [
    ["L1 · 1", 5],
    ["L2 · 1", 10],
    ["L3 · 1", 12],
  ],
  totalRows: 3,
  skip: 0,
};

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

const makeStudioArea = () => {
  const root = document.createElement("div");
  document.body.appendChild(root);
  const requested: string[] = [];
  const area = createPaneArea(root, {
    tableOnly: true,
    ctx: {
      requestTable: async (nodeId, port) => {
        requested.push(`${nodeId}.${port}`);
        return slice;
      },
      resolveAsset: (url) => url,
    },
    onSelect: () => {},
    onError: (m) => {
      throw new Error(m);
    },
  });
  return { root, area, requested };
};

const tabKinds = (root: HTMLElement) =>
  [...root.querySelectorAll<HTMLElement>(".bof-app-tab")].map((t) => t.dataset.kind);

describe("studioPanes", () => {
  it("offers the chart first for chart nodes and only the table otherwise", () => {
    expect(studioPanes(desc("chart.bar"))).toEqual(["chart", "table"]);
    expect(studioPanes(desc("chart.line"))).toEqual(["chart", "table"]);
    expect(studioPanes(desc("sql.query"))).toEqual(["table"]);
    expect(studioPanes(undefined)).toEqual(["table"]);
  });
});

describe("createPaneArea in the DuckDB studio", () => {
  it("shows no tab strip for a node without a chart", async () => {
    const { root, area } = makeStudioArea();
    area.showNode({ nodeId: "answer", desc: desc("sql.query"), values: {}, state: okState });
    await settle();
    expect(tabKinds(root)).toEqual([]);
    expect(root.querySelector<HTMLElement>(".bof-app-tabs")!.style.display).toBe("none");
    expect(root.querySelector("svg.bof-viz-bar-chart")).toBeNull();
    area.dispose();
  });

  it("opens a chart.bar node on its Chart tab, next to the Table tab", async () => {
    const { root, area, requested } = makeStudioArea();
    area.showNode({
      nodeId: "chart",
      desc: desc("chart.bar"),
      values: { labelColumn: "Storey", valueColumns: "Rooms", title: "Rooms per storey" },
      state: okState,
    });
    await settle();
    expect(tabKinds(root)).toEqual(["chart", "table"]);
    expect(root.querySelector<HTMLElement>(".bof-app-tabs")!.style.display).toBe("");
    expect(root.querySelector<HTMLElement>(".bof-app-tab-active")!.dataset.kind).toBe("chart");
    expect(root.querySelectorAll("rect.bof-viz-bar")).toHaveLength(3);
    expect(root.querySelector(".bof-viz-title")!.textContent).toBe("Rooms per storey");
    expect(requested).toEqual(["chart.table"]);

    root.querySelector<HTMLElement>('.bof-app-tab[data-kind="table"]')!.click();
    await settle();
    expect(root.querySelector("svg.bof-viz-bar-chart")).toBeNull();
    expect(root.querySelectorAll("tbody tr")).toHaveLength(3);
    area.dispose();
  });

  it("hides the strip again when the next node has no chart", async () => {
    const { root, area } = makeStudioArea();
    area.showNode({ nodeId: "chart", desc: desc("chart.line"), values: {}, state: okState });
    await settle();
    expect(tabKinds(root)).toEqual(["chart", "table"]);
    area.showNode({ nodeId: "answer", desc: desc("sql.query"), values: {}, state: okState });
    await settle();
    expect(tabKinds(root)).toEqual([]);
    expect(root.querySelector<HTMLElement>(".bof-app-tabs")!.style.display).toBe("none");
    area.dispose();
  });
});
