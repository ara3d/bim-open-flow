import { beforeEach, describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createSidebar } from "../src/sidebar.js";

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
  const opened: string[] = [];
  const added: NodeDescriptor[] = [];
  const sidebar = createSidebar(
    root,
    (id) => opened.push(id),
    (d) => added.push(d),
  );
  const search = root.querySelector("input") as HTMLInputElement;
  return { root, opened, added, sidebar, search };
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
    const { root, opened, added, sidebar } = setup();
    sidebar.setCatalog([desc("source.model"), desc("table.select")]);
    groupHeader(root, "table").click();
    expect(itemKinds(root)).toEqual(["table.select"]);
    const items = root.querySelectorAll(".bof-app-catalog .bof-app-item");
    (items[0] as HTMLElement).click();
    expect(added.map((d) => d.kind)).toEqual(["table.select"]);
    expect(opened).toEqual([]);
  });

  it("filtering opens only packs with a match, listing only matching nodes", () => {
    const { root, sidebar, search } = setup();
    sidebar.setCatalog([desc("table.select"), desc("csv.read"), desc("table.filter")]);
    search.value = "select";
    search.dispatchEvent(new Event("input"));
    expect(groupNames(root)).toEqual(["table"]);
    expect(itemKinds(root)).toEqual(["table.select"]);
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

  it("clicking an analysis entry opens it", () => {
    const { root, opened, added, sidebar } = setup();
    sidebar.setAnalyses(
      [{ id: "untitled-1", graphHash: "h" }],
      null,
    );
    (root.querySelector(".bof-app-analyses .bof-app-item") as HTMLElement).click();
    expect(opened).toEqual(["untitled-1"]);
    expect(added).toEqual([]);
  });

  it("places an empty Steps section between the flows and the catalog", () => {
    const { root, sidebar } = setup();
    const headers = [...root.querySelectorAll("h3")].map((h) => h.textContent);
    expect(headers).toEqual(["Flows", "Steps", "Node catalog"]);
    expect(sidebar.stepsEl.classList.contains("bof-app-steps")).toBe(true);
    expect(sidebar.stepsEl.previousElementSibling?.textContent).toBe("Steps");
    expect(sidebar.stepsEl.childElementCount).toBe(0);
    sidebar.setAnalyses([{ id: "untitled-1", graphHash: "h" }], null);
    sidebar.setCatalog([desc("table.select")]);
    expect(sidebar.stepsEl.childElementCount).toBe(0);
  });
});
