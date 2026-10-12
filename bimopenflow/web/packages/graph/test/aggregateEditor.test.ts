import { describe, expect, it } from "vitest";
import type { SuggestionList } from "@bimopenflow/contracts";
import { attachSuggestions } from "../src/suggestInput.js";
import { defaultName, formatAggregate, parseAggregates } from "../src/aggregateEditor.js";

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

const columns = (): SuggestionList => ({
  status: "Ok",
  values: [
    { value: "*", detail: "rows", functions: ["count"] },
    { value: "Volume", detail: "Number", functions: ["count", "sum", "min", "max", "avg"] },
    { value: "Name", detail: "Text", functions: ["count", "min", "max"] },
  ],
});

function open(value: string) {
  const input = document.createElement("input");
  input.value = value;
  document.body.appendChild(input);
  const changes: string[] = [];
  input.addEventListener("change", () => changes.push(input.value));
  const detach = attachSuggestions(input, "agg", async () => columns(), { aggregates: true });
  input.dispatchEvent(new Event("focus"));
  const popup = () => document.querySelector('[role="dialog"]') as HTMLElement;
  const el = <T extends HTMLElement>(label: string, n = 0) => popup().querySelectorAll<T>(`[aria-label="${label}"]`)[n]!;
  return { input, changes, detach, popup, el };
}

describe("aggregate text", () => {
  it("parses and formats func(column) as name, keeping what it cannot read", () => {
    expect(parseAggregates("count(*) as n, SUM(Volume) as total, junk")).toEqual([
      { func: "count", column: "*", name: "n" },
      { func: "sum", column: "Volume", name: "total" },
      { raw: "junk" },
    ]);
    expect(formatAggregate({ func: "avg", column: "Volume", name: "mean" })).toBe("avg(Volume) as mean");
    expect(defaultName("count", "*")).toBe("count");
    expect(defaultName("sum", "Volume")).toBe("sum_Volume");
  });
});

describe("aggregate builder", () => {
  it("shows a function, column and name picker per aggregate, not a free-text box", async () => {
    const { popup, el, detach } = open("sum(Volume) as total");
    await flush();
    expect(popup().hidden).toBe(false);
    expect(el<HTMLSelectElement>("Function").value).toBe("sum");
    expect(el<HTMLSelectElement>("Column").value).toBe("Volume");
    expect(el<HTMLInputElement>("Name").value).toBe("total");
    detach();
  });

  it("offers only the functions the column's type allows", async () => {
    const { el, detach } = open("min(Name) as first");
    await flush();
    const functions = [...el<HTMLSelectElement>("Function").options].map((o) => o.value);
    expect(functions).toEqual(["count", "min", "max"]);
    detach();
  });

  it("adds an aggregate and writes the text", async () => {
    const { el, changes, input, detach } = open("");
    await flush();
    el("Add aggregate").click();
    expect(input.value).toBe("count(*) as count");
    expect(changes).toEqual(["count(*) as count"]);
    detach();
  });

  it("changing the column keeps a function it allows, else takes its first, and renames a default name", async () => {
    const { el, input, detach } = open("sum(Volume) as sum_Volume");
    await flush();
    const column = el<HTMLSelectElement>("Column");
    column.value = "Name";
    column.dispatchEvent(new Event("change"));
    expect(input.value).toBe("count(Name) as count_Name");
    detach();
  });

  it("keeps a custom name and removes an aggregate", async () => {
    const { el, input, detach } = open("sum(Volume) as total,count(*) as n");
    await flush();
    const column = el<HTMLSelectElement>("Column");
    column.value = "Name";
    column.dispatchEvent(new Event("change"));
    expect(input.value).toBe("count(Name) as total,count(*) as n");
    el("Remove n").click();
    expect(input.value).toBe("count(Name) as total");
    detach();
  });
});
