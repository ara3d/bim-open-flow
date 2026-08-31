import { describe, expect, it } from "vitest";
import { createTablePane } from "../src/tablePane";
import { idColumnIndex } from "../src/columns";
import { conformance, tableInput } from "./conformance";
import { collect, fakeCtx, makeSlice, settle } from "./helpers";

conformance({ name: "TablePane", make: () => createTablePane(), input: tableInput });

const mountWith = (data = tableInput) => {
  const host = document.createElement("div");
  document.body.appendChild(host);
  const pane = createTablePane();
  const { events, handler } = collect();
  pane.onEvent(handler);
  pane.mount(host, fakeCtx());
  pane.update(data);
  return { host, pane, events };
};

describe("TablePane selection", () => {
  it("emits the globalId cell on row click", () => {
    const { host, pane, events } = mountWith();
    (host.querySelectorAll("tbody tr")[1] as HTMLElement).click();
    expect(events).toEqual([
      { kind: "selection", event: { source: "table", ids: ["g2"] } },
    ]);
    pane.destroy();
    host.remove();
  });

  it("falls back to entityId, then the first column", () => {
    const withEntity = makeSlice(
      [
        ["name", "Text"],
        ["entityId", "Integer"],
      ],
      [["Wall", 42]],
    );
    const { host, pane, events } = mountWith({ kind: "table", data: withEntity });
    (host.querySelector("tbody tr") as HTMLElement).click();
    expect(events[0]).toEqual({
      kind: "selection",
      event: { source: "table", ids: ["42"] },
    });

    pane.update({
      kind: "table",
      data: makeSlice([["name", "Text"], ["area", "Number"]], [["Slab", 7]]),
    });
    (host.querySelector("tbody tr") as HTMLElement).click();
    expect(events[1]).toEqual({
      kind: "selection",
      event: { source: "table", ids: ["Slab"] },
    });
    pane.destroy();
    host.remove();
  });

  it("heuristic prefers globalId over entityId", () => {
    expect(
      idColumnIndex([
        { name: "entityId", type: "Integer" },
        { name: "globalId", type: "Text" },
      ]),
    ).toBe(1);
  });

  it("highlights rows for an external selection input", () => {
    const { host, pane } = mountWith();
    pane.update({ kind: "selection", ids: ["g1"] });
    const rows = host.querySelectorAll("tbody tr");
    expect(rows[0].classList.contains("bof-panes-selected")).toBe(true);
    expect(rows[1].classList.contains("bof-panes-selected")).toBe(false);
    pane.destroy();
    host.remove();
  });

  it("keeps the highlight after the viz table re-sorts itself", async () => {
    const { host, pane } = mountWith();
    pane.update({ kind: "selection", ids: ["g1"] });
    const areaHeader = [...host.querySelectorAll("th")].find((th) =>
      th.textContent!.startsWith("area"),
    ) as HTMLElement;
    areaHeader.click(); // ascending: 2.5 first, so g1 is now the second row
    await settle();
    const rows = host.querySelectorAll("tbody tr");
    expect(rows[0].textContent).toContain("g2");
    expect(rows[0].classList.contains("bof-panes-selected")).toBe(false);
    expect(rows[1].classList.contains("bof-panes-selected")).toBe(true);
    pane.destroy();
    host.remove();
  });
});
