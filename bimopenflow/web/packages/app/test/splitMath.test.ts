import { describe, expect, it } from "vitest";
import { clampWidth, dragWidth, ghostX, type SplitSpec } from "../src/splitMath.js";

const left: SplitSpec = { min: 160, max: 520, sign: 1 };
const right: SplitSpec = { min: 240, max: 900, sign: -1 };

describe("clampWidth", () => {
  it("clamps to [min, max]", () => {
    expect(clampWidth(100, 160, 520)).toBe(160);
    expect(clampWidth(600, 160, 520)).toBe(520);
    expect(clampWidth(300, 160, 520)).toBe(300);
  });
});

describe("dragWidth", () => {
  it("grows the left column when dragging right", () => {
    expect(dragWidth(240, 240, 300, left)).toBe(300);
    expect(dragWidth(240, 240, 200, left)).toBe(200);
  });

  it("grows the right column when dragging left", () => {
    expect(dragWidth(420, 1000, 900, right)).toBe(520);
    expect(dragWidth(420, 1000, 1100, right)).toBe(320);
  });

  it("clamps at both limits", () => {
    expect(dragWidth(240, 240, 0, left)).toBe(160);
    expect(dragWidth(240, 240, 2000, left)).toBe(520);
    expect(dragWidth(420, 1000, 2000, right)).toBe(240);
    expect(dragWidth(420, 1000, 0, right)).toBe(900);
  });
});

describe("ghostX", () => {
  it("follows the pointer while unclamped", () => {
    const w = dragWidth(240, 240, 300, left);
    expect(ghostX(240, 240, w, left.sign)).toBe(300);
    const w2 = dragWidth(420, 1000, 950, right);
    expect(ghostX(1000, 420, w2, right.sign)).toBe(950);
  });

  it("pins at the limit when the width clamps", () => {
    const w = dragWidth(240, 240, 2000, left); // clamped to max 520
    expect(ghostX(240, 240, w, left.sign)).toBe(240 + (520 - 240));
  });
});
