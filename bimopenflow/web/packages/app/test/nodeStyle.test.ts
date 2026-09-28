import { beforeEach, describe, expect, it } from "vitest";
import { defaultNodeStyle } from "@bimopenflow/graph";
import { NODE_STYLE_PREF_KEY, loadNodeStyleChoice, saveNodeStyleChoice } from "../src/nodeStyleChoice.js";

// The seam (setNodeStyle, listeners) is tested in @bimopenflow/graph; this covers the persisted choice.

describe("node style choice persistence", () => {
  beforeEach(() => localStorage.clear());

  it("defaults when nothing is stored", () => {
    expect(loadNodeStyleChoice()).toBe(defaultNodeStyle);
  });

  it("round-trips a saved choice under bof-app-node-style", () => {
    saveNodeStyleChoice("chip");
    expect(localStorage.getItem("bof-app-node-style")).toBe("chip");
    expect(loadNodeStyleChoice()).toBe("chip");
  });

  it("falls back to the default on a foreign stored value", () => {
    localStorage.setItem(NODE_STYLE_PREF_KEY, "not-a-style");
    expect(loadNodeStyleChoice()).toBe(defaultNodeStyle);
  });
});
