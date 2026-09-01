import { describe, expect, it } from "vitest";
import type {
  NodeDescriptor,
  NodeState,
  ParamDescriptor,
  TableSlice,
} from "@bimopenflow/contracts";
import { createPaneArea } from "../src/paneArea.js";

const desc = (kind: string, params: ParamDescriptor[] = []): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params,
  description: "",
});

const okState: NodeState = { nodeId: "n1", status: "Ok", warnings: [] };

const slice: TableSlice = {
  columns: [
    { name: "name", type: "Text" },
    { name: "area", type: "Number" },
    { name: "count", type: "Integer" },
  ],
  rows: [
    ["a", 1, 2],
    ["b", 3, 4],
  ],
  totalRows: 2,
  skip: 0,
};

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

const makeArea = () => {
  const root = document.createElement("div");
  document.body.appendChild(root);
  const area = createPaneArea(root, {
    ctx: {
      requestTable: async () => slice,
      resolveAsset: (url) => url,
    },
    onSelect: () => {},
    onSetParam: () => {},
    onError: (m) => {
      throw new Error(m);
    },
  });
  return { root, area };
};

describe("createPaneArea 3D model wiring", () => {
  const view3dDesc: NodeDescriptor = {
    kind: "view3d.instances",
    version: 1,
    capability: "Pure",
    inputs: [],
    outputs: [{ name: "instances", type: "Table", optional: false }],
    params: [],
    description: "",
  };

  const makeView3dArea = (resolved: Record<string, string>) => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const updates: unknown[] = [];
    const area = createPaneArea(root, {
      ctx: { requestTable: async () => slice, resolveAsset: (url) => url },
      onSelect: () => {},
      onSetParam: () => {},
      onError: (m) => {
        throw new Error(m);
      },
      resolveModelId: async (path) => resolved[path] ?? null,
      paneFactory: () => ({
        mount: () => {},
        update: (input: unknown) => {
          updates.push(input);
        },
        onEvent: () => {},
        destroy: () => {},
      }),
    });
    return { area, updates };
  };

  const shownWith = (modelPath?: string) => ({
    nodeId: "n1",
    desc: view3dDesc,
    values: {},
    state: okState,
    modelPath,
  });

  it("pushes the model before the instances table, once per model", async () => {
    const { area, updates } = makeView3dArea({ "data/duplex.ifc": "duplex.ifc" });
    area.showNode(shownWith("data/duplex.ifc"));
    await settle();
    expect(updates).toEqual([
      { kind: "model", url: "model:duplex.ifc" },
      { kind: "instances", data: slice },
    ]);

    // a data refresh re-feeds the table but does not reload the model
    area.showNode(shownWith("data/duplex.ifc"));
    await settle();
    expect(updates.filter((u) => (u as { kind: string }).kind === "model")).toHaveLength(1);
    area.dispose();
  });

  it("feeds only the table when the model path is missing or unresolved", async () => {
    const { area, updates } = makeView3dArea({});
    area.showNode(shownWith("unknown.ifc"));
    await settle();
    expect(updates).toEqual([{ kind: "instances", data: slice }]);
    area.dispose();
  });
});

describe("createPaneArea chart wiring", () => {
  it("defaults chart.bar nodes to a bar chart built from its params", async () => {
    const { root, area } = makeArea();
    area.showNode({
      nodeId: "n1",
      desc: desc("chart.bar"),
      values: { labelColumn: "name", valueColumns: "area, count", title: "Areas" },
      state: okState,
    });
    await settle();
    const active = root.querySelector(".bof-app-tab-active") as HTMLElement;
    expect(active.dataset.kind).toBe("chart");
    expect(root.querySelector("svg.bof-viz-bar-chart")).not.toBeNull();
    expect(root.querySelector("text.bof-viz-title")?.textContent).toBe("Areas");
    const bars = [...root.querySelectorAll("rect.bof-viz-bar")];
    expect(bars.map((b) => b.getAttribute("data-series"))).toEqual([
      "area", "count", "area", "count",
    ]);
    area.dispose();
  });

  it("defaults chart.line nodes to a line chart built from its params", async () => {
    const { root, area } = makeArea();
    area.showNode({
      nodeId: "n1",
      desc: desc("chart.line"),
      values: { xColumn: "area", yColumns: "count", title: "Trend" },
      state: okState,
    });
    await settle();
    expect(root.querySelector("svg.bof-viz-line-chart")).not.toBeNull();
    expect(root.querySelector("text.bof-viz-title")?.textContent).toBe("Trend");
    const paths = [...root.querySelectorAll("path.bof-viz-line")];
    expect(paths.map((p) => p.getAttribute("data-series"))).toEqual(["count"]);
    area.dispose();
  });

  it("rebuilds the open chart pane when a param edit changes its options", async () => {
    const { root, area } = makeArea();
    const show = (values: Record<string, string>) =>
      area.showNode({ nodeId: "n1", desc: desc("chart.bar"), values, state: okState });
    show({ labelColumn: "name", valueColumns: "area", title: "Before" });
    await settle();
    expect(root.querySelector("text.bof-viz-title")?.textContent).toBe("Before");

    // title edit re-renders in place
    show({ labelColumn: "name", valueColumns: "area", title: "After" });
    await settle();
    expect(root.querySelector("text.bof-viz-title")?.textContent).toBe("After");

    // column edit swaps the plotted series instead of throwing on stale options
    show({ labelColumn: "name", valueColumns: "count", title: "After" });
    await settle();
    const bars = [...root.querySelectorAll("rect.bof-viz-bar")];
    expect(bars.map((b) => b.getAttribute("data-value"))).toEqual(["2", "4"]);

    // an unchanged re-show keeps the pane (still one chart svg, data refreshed)
    show({ labelColumn: "name", valueColumns: "count", title: "After" });
    await settle();
    expect(root.querySelectorAll("svg.bof-viz-bar-chart")).toHaveLength(1);
    area.dispose();
  });

  it("keeps the plain bar default for other table nodes", async () => {
    const { root, area } = makeArea();
    area.showNode({
      nodeId: "n1",
      desc: desc("table.select"),
      values: {},
      state: okState,
    });
    await settle();
    const active = root.querySelector(".bof-app-tab-active") as HTMLElement;
    expect(active.dataset.kind).toBe("table");
    const chartTab = [...root.querySelectorAll(".bof-app-tab")].find(
      (t) => (t as HTMLElement).dataset.kind === "chart",
    ) as HTMLElement;
    chartTab.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await settle();
    expect(root.querySelector("svg.bof-viz-bar-chart")).not.toBeNull();
    expect(root.querySelector("text.bof-viz-title")).toBeNull();
    // default series = all numeric columns, so both render grouped
    const bars = [...root.querySelectorAll("rect.bof-viz-bar")];
    expect(bars).toHaveLength(4);
    area.dispose();
  });
});
