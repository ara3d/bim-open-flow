// fitViewport: the framing GraphEditor.fit applies. The studio calls it with no
// options; a notebook cell passes a zoom floor, a uniform margin, and centred
// overflow (bim-open-notebook, embeds/graph.ts).

import { describe, expect, it } from "vitest";
import { contentBounds, fitViewport } from "../src/canvasEditor.js";

const view = { width: 728, height: 320 };
const cell = { minZoom: 0.6, margin: 16, overflow: "center" } as const;

/** Where the content's corners land in canvas pixels under a viewport. */
function screenRect(content: { x: number; y: number; width: number; height: number }, vp: ReturnType<typeof fitViewport>) {
  return {
    left: vp.pan.x + content.x * vp.zoom,
    top: vp.pan.y + content.y * vp.zoom,
    right: vp.pan.x + (content.x + content.width) * vp.zoom,
    bottom: vp.pan.y + (content.y + content.height) * vp.zoom,
  };
}

describe("contentBounds", () => {
  it("covers every node and is undefined for none", () => {
    expect(contentBounds([])).toBeUndefined();
    expect(contentBounds([{ x: 80, y: 40, w: 184, h: 94 }, { x: 400, y: -20, w: 260, h: 150 }]))
      .toEqual({ x: 80, y: -20, width: 580, height: 154 });
  });
});

describe("fitViewport", () => {
  it("scales a wide graph down to the width and centres it vertically", () => {
    const content = { x: 80, y: 80, width: 1000, height: 200 };
    const vp = fitViewport(content, view, cell);
    expect(vp.zoom).toBeCloseTo(696 / 1000);
    const r = screenRect(content, vp);
    expect(r.left).toBeCloseTo(16);
    expect(r.right).toBeCloseTo(712);
    expect((r.top + r.bottom) / 2).toBeCloseTo(160);
  });

  it("scales a tall graph down to the height and centres it horizontally", () => {
    const content = { x: 0, y: 0, width: 300, height: 700 };
    const vp = fitViewport(content, { width: 728, height: 560 }, cell);
    expect(vp.zoom).toBeCloseTo(528 / 700);
    const r = screenRect(content, vp);
    expect(r.top).toBeCloseTo(16);
    expect(r.bottom).toBeCloseTo(544);
    expect((r.left + r.right) / 2).toBeCloseTo(364);
  });

  it("holds the zoom floor and centres an overflowing graph on both axes", () => {
    const content = { x: 0, y: 0, width: 2000, height: 1000 };
    const vp = fitViewport(content, view, cell);
    expect(vp.zoom).toBe(0.6);
    const r = screenRect(content, vp);
    expect((r.left + r.right) / 2).toBeCloseTo(364);
    expect((r.top + r.bottom) / 2).toBeCloseTo(160);
    expect(r.left).toBeLessThan(0);
  });

  it("anchors an overflowing graph at the margin's start by default", () => {
    const content = { x: 50, y: 50, width: 2000, height: 1000 };
    const vp = fitViewport(content, view, { minZoom: 0.6, margin: 16 });
    const r = screenRect(content, vp);
    expect(r.left).toBeCloseTo(16);
    expect(r.top).toBeCloseTo(16);
  });

  it("never zooms a one-node graph past 1, and centres it", () => {
    const content = { x: 80, y: 80, width: 184, height: 94 };
    const vp = fitViewport(content, view, cell);
    expect(vp.zoom).toBe(1);
    const r = screenRect(content, vp);
    expect((r.left + r.right) / 2).toBeCloseTo(364);
    expect((r.top + r.bottom) / 2).toBeCloseTo(160);
  });

  it("keeps the studio's framing with no options: 24 px sides and bottom, 48 px top", () => {
    const content = { x: 0, y: 0, width: 1000, height: 800 };
    const vp = fitViewport(content, { width: 1048, height: 872 });
    expect(vp.zoom).toBe(1);
    expect(vp.pan).toEqual({ x: 24, y: 48 });
  });
});
