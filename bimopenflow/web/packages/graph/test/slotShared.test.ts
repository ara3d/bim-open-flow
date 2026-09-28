// styleIsland restyles an island input for the theme without disturbing the
// positioning gratify's runtime set on it once (position, offsets, transform
// origin): a theme switch used to rewrite the whole inline style, after which
// every island scaled about its centre and drifted away from its node.

import { describe, expect, it } from "vitest";
import { applyCanvasTheme, canvasThemes, defaultCanvasTheme } from "../src/canvasTheme.js";
import { islandKey, styleIsland } from "../src/slotShared.js";

describe("islandKey", () => {
  it("joins node and parameter with a double colon", () => {
    expect(islandKey("prices", "name")).toBe("prices::name");
  });
});

describe("styleIsland", () => {
  it("keeps the runtime's positioning while applying the theme's colours", () => {
    const el = document.createElement("input");
    el.style.position = "absolute";
    el.style.left = "0";
    el.style.top = "0";
    el.style.transformOrigin = "0 0";
    el.style.transform = "translate(28px, 293px) scale(0.48)";
    el.style.pointerEvents = "auto";
    el.style.width = "240px";
    el.style.height = "34px";

    styleIsland(el, canvasThemes.light.palette);

    expect(el.style.position).toBe("absolute");
    expect(el.style.transformOrigin).toBe("0 0");
    expect(el.style.transform).toBe("translate(28px, 293px) scale(0.48)");
    expect(el.style.pointerEvents).toBe("auto");
    expect(el.style.width).toBe("240px");
    expect(el.style.height).toBe("34px");
    expect(el.style.boxSizing).toBe("border-box");
    expect(el.style.borderRadius).toBe("5px");
    expect(el.style.color).not.toBe("");
    expect(el.style.backgroundColor).not.toBe("");
  });

  it("gives a fresh element the full width and height its island rect will scale", () => {
    const el = document.createElement("textarea");
    applyCanvasTheme("dark", true);
    try {
      styleIsland(el, canvasThemes.dark.palette);
    } finally {
      applyCanvasTheme(defaultCanvasTheme, true);
    }
    expect(el.style.width).toBe("100%");
    expect(el.style.height).toBe("100%");
    expect(el.style.colorScheme).toBe("dark");
  });
});
