import { describe, expect, it } from "vitest";
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
  return { root, opened, added, sidebar };
};

describe("sidebar catalog", () => {
  it("clicking a catalog entry dispatches the descriptor, not an open", () => {
    const { root, opened, added, sidebar } = setup();
    sidebar.setCatalog([desc("source.model"), desc("table.select")]);
    const items = root.querySelectorAll(".bof-app-catalog .bof-app-item");
    expect(items).toHaveLength(2);
    (items[1] as HTMLElement).click();
    expect(added.map((d) => d.kind)).toEqual(["table.select"]);
    expect(opened).toEqual([]);
  });

  it("groups entries by kind prefix, groups and kinds alphabetical", () => {
    const { root, sidebar } = setup();
    sidebar.setCatalog([
      desc("table.select"),
      desc("csv.read"),
      desc("table.filter"),
      desc("date.parse"),
    ]);
    const headers = [...root.querySelectorAll(".bof-app-catalog-group")].map((h) => h.textContent);
    expect(headers).toEqual(["csv", "date", "table"]);
    const kinds = [...root.querySelectorAll(".bof-app-catalog .bof-app-item")].map(
      (i) => i.firstChild?.textContent,
    );
    expect(kinds).toEqual(["csv.read", "date.parse", "table.filter", "table.select"]);
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
});
