import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  currentNodeStyle,
  defaultNodeStyle,
  isNodeStyleName,
  nodeStyleNames,
  onNodeStyleChange,
  setNodeStyle,
} from "../src/nodeStyle.js";
import { NODE_STYLE_PREF_KEY, loadNodeStyleChoice, saveNodeStyleChoice } from "../src/nodeStyleChoice.js";

afterEach(() => setNodeStyle(defaultNodeStyle));

describe("node style seam", () => {
  it("names four styles and validates names", () => {
    expect(nodeStyleNames).toEqual(["classic", "banner", "chip", "bar"]);
    expect(isNodeStyleName("chip")).toBe(true);
    expect(isNodeStyleName("fancy")).toBe(false);
  });

  it("notifies each listener once per change, and the returned function unsubscribes", () => {
    const listener = vi.fn();
    const off = onNodeStyleChange(listener);
    setNodeStyle("banner");
    expect(currentNodeStyle()).toBe("banner");
    expect(listener).toHaveBeenCalledTimes(1);

    setNodeStyle("banner");
    expect(listener).toHaveBeenCalledTimes(1);

    off();
    setNodeStyle("bar");
    expect(currentNodeStyle()).toBe("bar");
    expect(listener).toHaveBeenCalledTimes(1);
  });
});

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
