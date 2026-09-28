// The seam only: the persisted choice (nodeStyleChoice.ts) is the application's,
// beside its theme choice, and is tested there.

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  currentNodeStyle,
  defaultNodeStyle,
  isNodeStyleName,
  nodeStyleNames,
  onNodeStyleChange,
  setNodeStyle,
} from "../src/nodeStyle.js";

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
