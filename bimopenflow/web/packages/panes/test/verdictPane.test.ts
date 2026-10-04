import { describe, expect, it } from "vitest";
import { groupVerdicts, severityRank } from "../src/verdictGroups";
import { createVerdictPane } from "../src/verdictPane";
import { conformance } from "./conformance";
import { collect, fakeCtx, makeSlice } from "./helpers";

const verdictSlice = makeSlice(
  [
    ["globalId", "Text"],
    ["verdict", "Text"],
    ["checkId", "Text"],
    ["checkTitle", "Text"],
    ["citation", "Text"],
  ],
  [
    ["g1", "Pass", "NBC-1", "Doors", "9.5.1"],
    ["g2", "Fail", "NBC-1", "Doors", "9.5.1"],
    ["g3", "NeedsReview", "NBC-2", "Stairs", "9.8.4"],
    ["g4", "InfoNotAvailable", "NBC-2", "Stairs", "9.8.4"],
    ["g1", "Pass", "NBC-2", "Stairs", "9.8.4"],
  ],
);

conformance({
  name: "VerdictPane",
  make: () => createVerdictPane(),
  input: { kind: "table", data: verdictSlice },
});

describe("groupVerdicts", () => {
  it("groups by checkId in first-appearance order with counts and worst", () => {
    const groups = groupVerdicts(verdictSlice);
    expect(groups.map((g) => g.checkId)).toEqual(["NBC-1", "NBC-2"]);
    expect(groups[0].counts).toEqual({
      Pass: 1,
      Fail: 1,
      NeedsReview: 0,
      InfoNotAvailable: 0,
    });
    expect(groups[0].worst).toBe("Fail");
    expect(groups[1].worst).toBe("NeedsReview");
    expect(groups[1].rowIndices).toEqual([2, 3, 4]);
    expect(groups[1].checkTitle).toBe("Stairs");
    expect(groups[1].citation).toBe("9.8.4");
  });

  it("orders severity Fail > NeedsReview > InfoNotAvailable > Pass", () => {
    expect(severityRank("Fail")).toBeGreaterThan(severityRank("NeedsReview"));
    expect(severityRank("NeedsReview")).toBeGreaterThan(
      severityRank("InfoNotAvailable"),
    );
    expect(severityRank("InfoNotAvailable")).toBeGreaterThan(
      severityRank("Pass"),
    );
  });

  it("throws on a missing convention column or unknown verdict text", () => {
    expect(() =>
      groupVerdicts(makeSlice([["verdict", "Text"]], [["Pass"]])),
    ).toThrow(/missing column "checkId"/);
    const bad = {
      ...verdictSlice,
      rows: [["g1", "Maybe", "NBC-1", "Doors", "9.5.1"]],
      totalRows: 1,
    };
    expect(() => groupVerdicts(bad)).toThrow(/unknown verdict "Maybe"/);
  });
});

describe("VerdictPane rendering and events", () => {
  const mounted = () => {
    const host = document.createElement("div");
    const pane = createVerdictPane();
    const { events, handler } = collect();
    pane.onEvent(handler);
    pane.mount(host, fakeCtx());
    pane.update({ kind: "table", data: verdictSlice });
    return { host, pane, events };
  };

  it("renders one severity-colored block per check with count chips", () => {
    const { host, pane } = mounted();
    const blocks = host.querySelectorAll(".bof-panes-check");
    expect(blocks.length).toBe(2);
    expect(blocks[0].classList.contains("bof-panes-check--Fail")).toBe(true);
    expect(blocks[1].classList.contains("bof-panes-check--NeedsReview")).toBe(true);
    const chips = blocks[0].querySelectorAll(".bof-panes-chip");
    expect([...chips].map((c) => c.textContent)).toEqual(["Pass 1", "Fail 1"]);
    expect(blocks[0].textContent).toContain("NBC-1 — Doors");
    expect(blocks[0].textContent).toContain("9.5.1");
    pane.destroy();
  });

  it("emits the distinct row ids of a clicked check", () => {
    const { host, pane, events } = mounted();
    (host.querySelectorAll(".bof-panes-check")[1] as HTMLElement).click();
    expect(events).toEqual([
      {
        kind: "selection",
        event: { source: "verdict", ids: ["g3", "g4", "g1"] },
      },
    ]);
    pane.destroy();
  });

  it("highlights checks containing an externally selected id", () => {
    const { host, pane } = mounted();
    pane.update({ kind: "selection", ids: ["g3"] });
    const blocks = host.querySelectorAll(".bof-panes-check");
    expect(blocks[0].classList.contains("bof-panes-check--selected")).toBe(false);
    expect(blocks[1].classList.contains("bof-panes-check--selected")).toBe(true);
    pane.destroy();
  });
});
