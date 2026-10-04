import { beforeEach, describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createSidebar, tabForFlow } from "../src/sidebar.js";

const desc = (kind: string): NodeDescriptor => ({
  kind,
  version: 1,
  capability: "Pure",
  inputs: [],
  outputs: [],
  params: [],
  description: `about ${kind}`,
});

const setup = () => {
  const root = document.createElement("div");
  const added: NodeDescriptor[] = [];
  const sidebar = createSidebar(root, (d) => added.push(d));
  const search = root.querySelector("input") as HTMLInputElement;
  return { root, added, sidebar, search };
};

const groupNames = (root: HTMLElement) =>
  [...root.querySelectorAll(".bof-app-catalog-group-name")].map((h) => h.textContent);

const groupHeader = (root: HTMLElement, pack: string) =>
  [...root.querySelectorAll(".bof-app-catalog-group")].find(
    (h) => h.querySelector(".bof-app-catalog-group-name")?.textContent === pack,
  ) as HTMLElement;

const itemKinds = (root: HTMLElement) =>
  [...root.querySelectorAll(".bof-app-catalog .bof-app-item")].map((i) => i.firstChild?.textContent);

describe("sidebar catalog", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("opens every pack collapsed with a count, and shows no nodes", () => {
    const { root, sidebar } = setup();
    sidebar.setCatalog([
      desc("table.select"),
      desc("csv.read"),
      desc("table.filter"),
      desc("date.parse"),
    ]);
    expect(groupNames(root)).toEqual(["csv", "date", "table"]);
    expect(itemKinds(root)).toEqual([]);
    const table = groupHeader(root, "table");
    expect(table.getAttribute("aria-expanded")).toBe("false");
    expect(table.querySelector(".bof-app-catalog-group-count")?.textContent).toBe("2");
  });

  it("clicking a pack header expands it, and clicking a node dispatches its descriptor", () => {
    const { root, added, sidebar } = setup();
    sidebar.setCatalog([desc("source.model"), desc("table.select")]);
    groupHeader(root, "table").click();
    expect(itemKinds(root)).toEqual(["table.select"]);
    const items = root.querySelectorAll(".bof-app-catalog .bof-app-item");
    (items[0] as HTMLElement).click();
    expect(added.map((d) => d.kind)).toEqual(["table.select"]);
  });

  it("filtering opens only packs with a match, listing only matching nodes", () => {
    const { root, sidebar, search } = setup();
    sidebar.setCatalog([desc("table.select"), desc("csv.read"), desc("table.filter")]);
    search.value = "select";
    search.dispatchEvent(new Event("input"));
    expect(groupNames(root)).toEqual(["table"]);
    expect(itemKinds(root)).toEqual(["table.select"]);
  });

  it("shows a triangle that follows aria-expanded, including filter-opened packs", () => {
    const { root, sidebar, search } = setup();
    sidebar.setCatalog([desc("table.select"), desc("csv.read")]);
    const triangle = (pack: string) =>
      groupHeader(root, pack).querySelector(".bof-app-catalog-group-triangle")?.textContent;
    expect(triangle("csv")).toBe("▸");
    groupHeader(root, "csv").click();
    expect(triangle("csv")).toBe("▾");
    search.value = "select";
    search.dispatchEvent(new Event("input"));
    expect(groupHeader(root, "table").getAttribute("aria-expanded")).toBe("true");
    expect(triangle("table")).toBe("▾");
  });

  it("clearing the filter restores the packs the user had open before typing", () => {
    const { root, sidebar, search } = setup();
    sidebar.setCatalog([desc("table.select"), desc("csv.read"), desc("table.filter")]);
    groupHeader(root, "csv").click();
    search.value = "select";
    search.dispatchEvent(new Event("input"));
    search.value = "";
    search.dispatchEvent(new Event("input"));
    expect(groupHeader(root, "csv").getAttribute("aria-expanded")).toBe("true");
    expect(groupHeader(root, "table").getAttribute("aria-expanded")).toBe("false");
  });

  it("persists the expanded set through prefs across a reload", () => {
    const first = setup();
    first.sidebar.setCatalog([desc("table.select"), desc("csv.read")]);
    groupHeader(first.root, "table").click();

    const second = setup();
    second.sidebar.setCatalog([desc("table.select"), desc("csv.read")]);
    expect(groupHeader(second.root, "table").getAttribute("aria-expanded")).toBe("true");
    expect(groupHeader(second.root, "csv").getAttribute("aria-expanded")).toBe("false");
  });

});

const tabLabels = (root: HTMLElement) =>
  [...root.querySelectorAll(".bof-app-tabs .bof-app-tab")].map((t) => t.textContent);

const activeTab = (root: HTMLElement) =>
  root.querySelector(".bof-app-tab-active")?.textContent;

const tab = (root: HTMLElement, label: string) =>
  [...root.querySelectorAll(".bof-app-tab")].find((t) => t.textContent === label) as HTMLElement;

const visiblePanels = (root: HTMLElement) =>
  [...root.querySelectorAll<HTMLElement>(".bof-app-sidebar-panel")].filter((p) => !p.hidden)
    .map((p) => p.getAttribute("aria-label"));

describe("sidebar tabs", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("shows two tabs, Steps and Nodes, and lists no flows", () => {
    const { root, sidebar } = setup();
    expect(tabLabels(root)).toEqual(["Steps", "Nodes"]);
    expect(root.querySelector(".bof-app-analyses")).toBeNull();
    expect(root.querySelector("h3")).toBeNull();
    expect(sidebar.stepsEl.closest(".bof-app-sidebar-panel")?.getAttribute("aria-label")).toBe("Steps");
    expect(sidebar.stepsEl.childElementCount).toBe(0);
    expect(root.querySelector("input")!.closest(".bof-app-sidebar-panel")?.getAttribute("aria-label")).toBe("Nodes");
  });

  it("opens a flow with nodes on Steps and an empty flow on Nodes when nothing is remembered", () => {
    const { root, sidebar } = setup();
    sidebar.flowOpened(true);
    expect(activeTab(root)).toBe("Steps");
    expect(visiblePanels(root)).toEqual(["Steps"]);
    sidebar.flowOpened(false);
    expect(activeTab(root)).toBe("Nodes");
    expect(visiblePanels(root)).toEqual(["Nodes"]);
    expect(tab(root, "Nodes").getAttribute("aria-selected")).toBe("true");
    expect(tab(root, "Steps").getAttribute("aria-selected")).toBe("false");
  });

  it("keeps the tab the user picked, across flows and reloads", () => {
    const first = setup();
    tab(first.root, "Nodes").click();
    first.sidebar.flowOpened(true);
    expect(activeTab(first.root)).toBe("Nodes");

    const second = setup();
    second.sidebar.flowOpened(true);
    expect(activeTab(second.root)).toBe("Nodes");
    tab(second.root, "Steps").click();
    second.sidebar.flowOpened(false);
    expect(activeTab(second.root)).toBe("Steps");
  });

  it("showTab switches without remembering the choice", () => {
    const { root, sidebar } = setup();
    sidebar.showTab("nodes");
    expect(activeTab(root)).toBe("Nodes");
    sidebar.flowOpened(true);
    expect(activeTab(root)).toBe("Steps");
  });

  it("tabForFlow ignores an unknown remembered value", () => {
    expect(tabForFlow("flows", true)).toBe("steps");
    expect(tabForFlow(null, false)).toBe("nodes");
    expect(tabForFlow("steps", false)).toBe("steps");
  });
});
