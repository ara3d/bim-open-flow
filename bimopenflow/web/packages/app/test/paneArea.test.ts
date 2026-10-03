import { describe, expect, it } from "vitest";
import type {
  NodeDescriptor,
  NodeState,
  ParamDescriptor,
  TableSlice,
} from "@bimopenflow/contracts";
import type { PaneEvent } from "@bimopenflow/panes";
import { genericPanes, paneRegistry, type PaneRegistration } from "@bimopenflow/client";
import { createPaneArea, type PaneArea } from "../src/paneArea.js";

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
    onError: (m) => {
      throw new Error(m);
    },
  });
  return { root, area };
};

describe("createPaneArea with registered panes", () => {
  const recipeDesc: NodeDescriptor = { ...desc("view.recipe"), outputs: [{ name: "view", type: "Table", optional: false }] };

  /** A registration whose panes record their updates; it keeps its pane across nodes of one model. */
  const recorder = () => {
    const created: unknown[][] = [];
    const fed: string[] = [];
    let emit: (e: PaneEvent) => void = () => {};
    const registration: PaneRegistration = {
      kind: "rec",
      label: "Recorder",
      offer: (d) => (d?.kind === "view.recipe" ? 0 : undefined),
      create: () => {
        const updates: unknown[] = [];
        created.push(updates);
        return { mount: () => {}, update: (i) => { updates.push(i); }, onEvent: (h) => { emit = h; }, destroy: () => {} };
      },
      fillHeight: true,
      feed: async (pane, shown, io) => {
        fed.push(`${shown.nodeId}:${io.port?.name}`);
        if (io.current()) pane.update({ kind: "table", data: slice });
      },
      keep: (prev, next) => prev.modelPath === next.modelPath,
    };
    return { registration, created, fed, emit: (e: PaneEvent) => emit(e) };
  };

  const makeRegisteredArea = (registration: PaneRegistration) => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const errors: string[] = [];
    const area = createPaneArea(root, {
      ctx: { requestTable: async () => slice, resolveAsset: (url) => url },
      onSelect: () => {},
      onError: (m) => { errors.push(m); },
      panes: paneRegistry(...genericPanes, registration),
    });
    return { root, area, errors };
  };

  const show = (area: PaneArea, nodeId: string, modelPath: string) =>
    area.showNode({ nodeId, modelPath, desc: recipeDesc, values: {}, state: { ...okState, nodeId } });

  it("offers a registered pane first by its offer, labelled and full height", async () => {
    const rec = recorder();
    const { root, area } = makeRegisteredArea(rec.registration);
    show(area, "n1", "snowdon");
    await settle();
    const tabs = [...root.querySelectorAll<HTMLElement>(".bof-app-tab")];
    expect(tabs.map((t) => t.dataset.kind)).toEqual(["rec", "table", "chart", "inspector"]);
    expect(tabs[0]!.textContent).toBe("Recorder");
    expect(root.querySelector<HTMLElement>(".bof-app-panebody > div")!.style.height).toBe("100%");
    expect(rec.fed).toEqual(["n1:view"]);
    area.dispose();
  });

  it("keeps the mounted pane when its registration says so, and rebuilds it otherwise", async () => {
    const rec = recorder();
    const { area } = makeRegisteredArea(rec.registration);
    show(area, "categories", "snowdon");
    await settle();
    show(area, "cutaway", "snowdon");
    await settle();
    expect(rec.created).toHaveLength(1);
    expect(rec.fed).toEqual(["categories:view", "cutaway:view"]);
    show(area, "other", "duplex");
    await settle();
    expect(rec.created).toHaveLength(2);
    area.dispose();
  });

  it("reports a pane's loadError with the pane's label", async () => {
    const rec = recorder();
    const { area, errors } = makeRegisteredArea(rec.registration);
    show(area, "n1", "snowdon");
    await settle();
    rec.emit({ kind: "action", action: "loadError", payload: { message: "bad bytes" } });
    expect(errors).toEqual(["Recorder model load failed: bad bytes"]);
    area.dispose();
  });

  it("offers only the generic panes when nothing else is registered", async () => {
    const { root, area } = makeArea();
    area.showNode({ nodeId: "n1", desc: { ...desc("view3d.instances"), outputs: [{ name: "instances", type: "Table", optional: false }] }, values: {}, state: okState });
    await settle();
    expect([...root.querySelectorAll<HTMLElement>(".bof-app-tab")].map((t) => t.dataset.kind)).toEqual(["table", "chart", "inspector"]);
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

describe("createPaneArea default-shown header (TKT-46/TKT-81)", () => {
  it("names the node as the flow's answer, never 'nothing selected'", async () => {
    const { root, area } = makeArea();
    area.showNode({
      nodeId: "n1",
      desc: desc("table.select"),
      values: {},
      state: okState,
      default: true,
    });
    await settle();
    expect(root.querySelector(".bof-app-preview-source")?.textContent).toBe(
      "Answer: table.select (n1) · live graph output",
    );
    expect(root.querySelector(".bof-app-back-to-answer")).toHaveProperty("hidden", true);
    area.dispose();
  });

  it("shows 'Showing ...' with a Back to answer button once explicitly overridden", async () => {
    const { root, area } = makeArea();
    area.showNode({ nodeId: "n1", desc: desc("table.select"), values: {}, state: okState, default: false });
    await settle();
    const text = root.querySelector(".bof-app-preview-source")?.textContent ?? "";
    expect(text.startsWith("Showing table.select (n1)")).toBe(true);
    expect(text).not.toContain("nothing selected");
    expect(root.querySelector(".bof-app-back-to-answer")).toHaveProperty("hidden", false);
    area.dispose();
  });

  it("shows a pin toggle reflecting the pinned flag", async () => {
    const { root, area } = makeArea();
    area.showNode({ nodeId: "n1", desc: desc("table.select"), values: {}, state: okState, pinned: true });
    await settle();
    const pin = root.querySelector(".bof-app-pin-toggle");
    expect(pin).toHaveProperty("hidden", false);
    expect(pin?.getAttribute("aria-pressed")).toBe("true");
    area.dispose();
  });
});

describe("createPaneArea result reads", () => {
  it("shows a failed result read on the header line, not as an error", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const errors: string[] = [];
    const area = createPaneArea(root, {
      ctx: {
        requestTable: async () => {
          throw new Error(`GET /api/analyses/a/results/n1/out -> 404: {"error":"Node 'n1' not in analysis 'a'"}`);
        },
        resolveAsset: (url) => url,
      },
      onSelect: () => {},
      onError: (m) => { errors.push(m); },
    });
    area.showNode({ nodeId: "n1", desc: desc("table.filter"), values: {}, state: okState });
    await settle();
    expect(errors).toEqual([]);
    expect(root.querySelector(".bof-app-preview-source")?.textContent)
      .toContain("No rows to show: Node 'n1' not in analysis 'a'");
    area.dispose();
  });

  it("drops a failed read that a newer node selection superseded", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    let rejectFirst: (e: Error) => void = () => {};
    const area = createPaneArea(root, {
      ctx: {
        requestTable: (nodeId) => nodeId === "n1"
          ? new Promise((_, reject) => { rejectFirst = reject; })
          : Promise.resolve(slice),
        resolveAsset: (url) => url,
      },
      onSelect: () => {},
      onError: (m) => { throw new Error(m); },
    });
    area.showNode({ nodeId: "n1", desc: desc("table.filter"), values: {}, state: okState });
    area.showNode({ nodeId: "n2", desc: desc("table.filter"), values: {}, state: { ...okState, nodeId: "n2" } });
    rejectFirst(new Error("GET ... -> 404: gone"));
    await settle();
    expect(root.querySelector(".bof-app-preview-source")?.textContent).not.toContain("No rows");
    area.dispose();
  });
});
