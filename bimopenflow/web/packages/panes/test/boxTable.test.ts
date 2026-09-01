// Headless tests for boxes-table parsing and the unit-cube buffers.
import { describe, expect, it } from "vitest";
import { parseBoxTable, UNIT_CUBE } from "../src/boxTable";
import { makeSlice } from "./helpers";

const BOUNDS: Array<[string, "Number"]> = [
  ["minX", "Number"],
  ["minY", "Number"],
  ["minZ", "Number"],
  ["maxX", "Number"],
  ["maxY", "Number"],
  ["maxZ", "Number"],
];

describe("parseBoxTable", () => {
  it("scales the unit cube to the extent and translates to the center", () => {
    const plan = parseBoxTable(makeSlice(BOUNDS, [[1, 2, 3, 5, 8, 4]]));
    expect(plan.count).toBe(1);
    const m = [...plan.transforms];
    expect(m[0]).toBe(4); // extent x
    expect(m[5]).toBe(6); // extent y
    expect(m[10]).toBe(1); // extent z
    expect(m[12]).toBe(3); // center x
    expect(m[13]).toBe(5); // center y
    expect(m[14]).toBe(3.5); // center z
    expect(m[15]).toBe(1);
    // everything off the scale/translation slots is zero
    for (const i of [1, 2, 3, 4, 6, 7, 8, 9, 11]) expect(m[i]).toBe(0);
  });

  it("defaults colors to gray without all of r/g/b/a", () => {
    const plan = parseBoxTable(
      makeSlice([...BOUNDS, ["r", "Number"]], [[0, 0, 0, 1, 1, 1, 0.9]]),
    );
    const gray = Math.fround(0.7);
    expect([...plan.colors]).toEqual([gray, gray, gray, 1]);
  });

  it("reads r/g/b/a colors when all four are present", () => {
    const plan = parseBoxTable(
      makeSlice(
        [...BOUNDS, ["r", "Number"], ["g", "Number"], ["b", "Number"], ["a", "Number"]],
        [
          [0, 0, 0, 1, 1, 1, 1, 0, 0, 0.5],
          [0, 0, 0, 2, 2, 2, 0, 1, 0, 1],
        ],
      ),
    );
    expect(plan.count).toBe(2);
    expect([...plan.colors.slice(0, 4)]).toEqual([1, 0, 0, 0.5]);
    expect([...plan.colors.slice(4, 8)]).toEqual([0, 1, 0, 1]);
  });

  it("throws naming the missing bounds columns", () => {
    expect(() =>
      parseBoxTable(makeSlice([["minX", "Number"], ["label", "Text"]], [])),
    ).toThrow(/missing required column\(s\): minY, minZ, maxX, maxY, maxZ/);
  });
});

describe("UNIT_CUBE", () => {
  it("is a flat-shaded cube: 24 vertices, 12 triangles, edge 1, centered", () => {
    expect(UNIT_CUBE.positions.length).toBe(24 * 3);
    expect(UNIT_CUBE.normals!.length).toBe(24 * 3);
    expect(UNIT_CUBE.indices!.length).toBe(36);
    for (const p of UNIT_CUBE.positions) expect(Math.abs(p)).toBe(0.5);
    for (const i of UNIT_CUBE.indices!) expect(i).toBeLessThan(24);
    // each vertex normal is a unit axis vector
    for (let v = 0; v < 24; v++) {
      const n = [...UNIT_CUBE.normals!.slice(v * 3, v * 3 + 3)];
      expect(Math.abs(n[0]) + Math.abs(n[1]) + Math.abs(n[2])).toBe(1);
    }
  });
});
