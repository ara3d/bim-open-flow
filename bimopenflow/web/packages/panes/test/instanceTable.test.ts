// Headless tests for the 3D pane's pure color/isolation/pick-mapping logic.
import { describe, expect, it } from "vitest";
import {
  entityForPick,
  groupColorPlan,
  instanceKeyColumn,
  planFromSlice,
  type ColorableGroup,
  type GroupEntityMap,
} from "../src/instanceTable";
import { makeSlice } from "./helpers";

const fakeGroup = (colors: number[]): ColorableGroup => ({
  instanceCount: colors.length / 4,
  colors: Float32Array.from(colors),
  setColors: () => {},
});

describe("planFromSlice", () => {
  it("keys by entityId and reads r/g/b/a colors", () => {
    const plan = planFromSlice(
      makeSlice(
        [
          ["entityId", "Integer"],
          ["r", "Number"],
          ["g", "Number"],
          ["b", "Number"],
          ["a", "Number"],
        ],
        [
          [7, 1, 0, 0, 1],
          [9, 0, 1, 0, 0.5],
        ],
      ),
    );
    expect([...plan.keys]).toEqual([7, 9]);
    expect(plan.colors!.get(9)).toEqual([0, 1, 0, 0.5]);
  });

  it("falls back to instanceIndex and yields no colors without all of r/g/b/a", () => {
    const plan = planFromSlice(
      makeSlice(
        [
          ["instanceIndex", "Integer"],
          ["r", "Number"],
        ],
        [[3, 1]],
      ),
    );
    expect([...plan.keys]).toEqual([3]);
    expect(plan.colors).toBeNull();
  });

  it("throws when neither key column exists", () => {
    expect(() =>
      planFromSlice(makeSlice([["name", "Text"]], [["Wall"]])),
    ).toThrow(/entityId.*instanceIndex/);
  });
});

describe("groupColorPlan", () => {
  const base = Float32Array.from([
    0.1, 0.2, 0.3, 1, // entity 10
    0.4, 0.5, 0.6, 1, // entity 20
    0.7, 0.8, 0.9, 1, // entity 30
  ]);
  const entities = [10, 20, 30];

  it("recolors planned entities, hides absent ones via alpha 0", () => {
    const plan = {
      keys: new Set([10, 30]),
      colors: new Map([[10, [1, 0, 0, 1] as const]]),
    };
    const out = groupColorPlan(entities, base, plan)!;
    expect([...out.slice(0, 4)]).toEqual([1, 0, 0, 1]); // planned color
    // entity 20 absent from the plan: base rgb kept, alpha forced to 0
    expect([...out.slice(4, 8)]).toEqual([base[4], base[5], base[6], 0]);
    // entity 30 in the plan without a color: base color kept
    expect([...out.slice(8, 12)]).toEqual([base[8], base[9], base[10], base[11]]);
  });

  it("restores base colors when a later plan includes everything", () => {
    const plan = { keys: new Set(entities), colors: null };
    const out = groupColorPlan(entities, base, plan)!;
    expect([...out]).toEqual([...base]);
  });

  it("returns null for an empty group", () => {
    expect(groupColorPlan([], base, { keys: new Set(), colors: null })).toBeNull();
  });
});

describe("entityForPick", () => {
  it("maps (group, instanceIndex) to the loader's entity id", () => {
    const g1 = fakeGroup([0, 0, 0, 1]);
    const g2 = fakeGroup([0, 0, 0, 1, 0, 0, 0, 1]);
    const maps: GroupEntityMap[] = [
      { group: g1, entities: [11] },
      { group: g2, entities: [22, 33] },
    ];
    expect(entityForPick(maps, g2, 1)).toBe(33);
    expect(entityForPick(maps, g1, 0)).toBe(11);
    expect(entityForPick(maps, fakeGroup([]), 0)).toBeUndefined();
  });
});
