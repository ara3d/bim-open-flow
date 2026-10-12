// TKT-279: the left sidebar opens at about 200 px, drags down to 160, and a
// width stored before is still honoured.

import { beforeEach, describe, expect, it } from "vitest";
import { LEFT_SPLIT, restoreWidth } from "../src/columnSplitter.js";

const leftWidth = () => {
  const root = document.createElement("div");
  document.body.append(root);
  restoreWidth(root, LEFT_SPLIT);
  const width = root.style.getPropertyValue(LEFT_SPLIT.cssVar);
  root.remove();
  return width;
};

describe("the left column (TKT-279)", () => {
  beforeEach(() => localStorage.clear());

  it("opens at 200 px on a fresh browser and drags down to 160", () => {
    expect(leftWidth()).toBe("200px");
    expect(LEFT_SPLIT.min).toBe(160);
  });

  it("honours a stored width, clamped to the least", () => {
    localStorage.setItem(LEFT_SPLIT.storageKey, "300");
    expect(leftWidth()).toBe("300px");
    localStorage.setItem(LEFT_SPLIT.storageKey, "120");
    expect(leftWidth()).toBe("160px");
  });
});
