import { describe, expect, it } from "vitest";
import type { TableData } from "@bimopenflow/contracts";
import { DataTableView } from "../src/table";

const data: TableData = {
  columns: [
    { name: "Name", type: "Text"},
    { name: "Count", type: "Integer"},
    { name: "Ratio", type: "Number"},
  ],
  rows: [
    ["Wall", 3, 0.5],
    ["Door", 1, 2.25],
    ["Slab", 2, null],
  ],
};

const mountTable = (d: TableData = data, options = {}) => {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const handle = DataTableView.mount(container, d, options);
  return { container, handle };
};

const cellTexts = (container: HTMLElement, column: number): string[] =>
  [...container.querySelectorAll("tbody tr")].map(
    (tr) => tr.children[column].textContent ?? "",
  );

describe("DataTableView", () => {
  it("renders headers with numeric alignment", () => {
    const { container } = mountTable();
    const ths = [...container.querySelectorAll("th")];
    expect(ths.map((t) => t.textContent)).toEqual(["Name", "Count", "Ratio"]);
    expect(ths[0].classList.contains("bof-viz-num")).toBe(false);
    expect(ths[1].classList.contains("bof-viz-num")).toBe(true);
    expect(ths[2].classList.contains("bof-viz-num")).toBe(true);
  });

  it("renders all rows with invariant formatting and empty nulls", () => {
    const { container } = mountTable();
    expect(container.querySelectorAll("tbody tr")).toHaveLength(3);
    expect(cellTexts(container, 2)).toEqual(["0.5", "2.25", ""]);
    const numericCell = container.querySelector("tbody td.bof-viz-num");
    expect(numericCell?.textContent).toBe("3");
  });

  it("sorts by column on header click, toggles direction, nulls last", () => {
    const { container } = mountTable();
    const countHeader = [...container.querySelectorAll("th")][1];
    countHeader.click();
    expect(cellTexts(container, 1)).toEqual(["1", "2", "3"]);
    // re-query: render replaces the header elements
    (container.querySelector('th[data-column="Count"]') as HTMLElement).click();
    expect(cellTexts(container, 1)).toEqual(["3", "2", "1"]);
    (container.querySelector('th[data-column="Ratio"]') as HTMLElement).click();
    expect(cellTexts(container, 2)).toEqual(["0.5", "2.25", ""]);
    (container.querySelector('th[data-column="Ratio"]') as HTMLElement).click();
    expect(cellTexts(container, 2)).toEqual(["2.25", "0.5", ""]);
  });

  it("caps rows and shows the footer", () => {
    const big: TableData = {
      columns: [{ name: "n", type: "Integer"}],
      rows: Array.from({ length: 12 }, (_, i) => [i]),
    };
    const { container } = mountTable(big, { maxRows: 5 });
    expect(container.querySelectorAll("tbody tr")).toHaveLength(5);
    expect(container.querySelector(".bof-viz-footer")?.textContent).toBe(
      "showing 5 of 12 rows",
    );
  });

  it("shows no footer when under the cap", () => {
    const { container } = mountTable();
    expect(container.querySelector(".bof-viz-footer")).toBeNull();
  });

  it("update() re-renders with new data", () => {
    const { container, handle } = mountTable();
    handle.update({
      columns: [{ name: "Only", type: "Text"}],
      rows: [["x"], ["y"]],
    });
    expect(
      [...container.querySelectorAll("th")].map((t) => t.textContent),
    ).toEqual(["Only"]);
    expect(container.querySelectorAll("tbody tr")).toHaveLength(2);
  });

  it("destroy() removes everything it added", () => {
    const { container, handle } = mountTable();
    handle.destroy();
    expect(container.children).toHaveLength(0);
  });
});
