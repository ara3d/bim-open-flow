// Headless tests for the 3D pane's pure color/isolation/pick-mapping logic.
import { describe, expect, it } from "vitest";
import {
  entityForPick,
  groupColorPlan,
  groupTransformPlan,
  instanceKeyColumn,
  planFromSlice,
  type ColorableGroup,
  type GroupEntityMap,
  type InstancePlan,
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
    // colors carry alpha already; alphas mirror the a column
    expect(plan.alphas!.get(9)).toBe(0.5);
    expect(plan.offsets).toBeNull();
  });

  it("builds alphas from an a column alone, without colors", () => {
    const plan = planFromSlice(
      makeSlice(
        [
          ["entityId", "Integer"],
          ["a", "Number"],
        ],
        [
          [7, 0],
          [9, 0.25],
        ],
      ),
    );
    expect(plan.colors).toBeNull();
    expect(plan.alphas!.get(7)).toBe(0);
    expect(plan.alphas!.get(9)).toBe(0.25);
  });

  it("builds offsets from offsetX/Y/Z, zeroing non-finite cells", () => {
    const plan = planFromSlice(
      makeSlice(
        [
          ["entityId", "Integer"],
          ["offsetX", "Number"],
          ["offsetY", "Number"],
          ["offsetZ", "Number"],
        ],
        [
          [7, 1, 2, 3],
          [9, "bogus", -4, null],
        ],
      ),
    );
    expect(plan.offsets!.get(7)).toEqual([1, 2, 3]);
    expect(plan.offsets!.get(9)).toEqual([0, -4, 0]);
    expect(plan.alphas).toBeNull();
  });

  it("yields no offsets when any offset column is missing", () => {
    const plan = planFromSlice(
      makeSlice(
        [
          ["entityId", "Integer"],
          ["offsetX", "Number"],
          ["offsetY", "Number"],
        ],
        [[7, 1, 2]],
      ),
    );
    expect(plan.offsets).toBeNull();
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
  const plan = (p: Partial<InstancePlan>): InstancePlan => ({
    keys: new Set(),
    colors: null,
    alphas: null,
    offsets: null,
    ...p,
  });

  it("recolors planned entities, hides absent ones via alpha 0", () => {
    const p = plan({
      keys: new Set([10, 30]),
      colors: new Map([[10, [1, 0, 0, 1] as const]]),
    });
    const out = groupColorPlan(entities, base, p)!;
    expect([...out.slice(0, 4)]).toEqual([1, 0, 0, 1]); // planned color
    // entity 20 absent from the plan: base rgb kept, alpha forced to 0
    expect([...out.slice(4, 8)]).toEqual([base[4], base[5], base[6], 0]);
    // entity 30 in the plan without a color: base color kept
    expect([...out.slice(8, 12)]).toEqual([base[8], base[9], base[10], base[11]]);
  });

  it("applies alpha-only plans as base rgb with the plan alpha", () => {
    const p = plan({
      keys: new Set(entities),
      alphas: new Map([
        [10, 0.5],
        [20, 0],
      ]),
    });
    const out = groupColorPlan(entities, base, p)!;
    expect([...out.slice(0, 4)]).toEqual([base[0], base[1], base[2], 0.5]);
    expect([...out.slice(4, 8)]).toEqual([base[4], base[5], base[6], 0]);
    // entity 30 has no alpha entry: base color kept
    expect([...out.slice(8, 12)]).toEqual([base[8], base[9], base[10], base[11]]);
  });

  it("lets a plan color win over the alpha map", () => {
    const p = plan({
      keys: new Set([10]),
      colors: new Map([[10, [1, 0, 0, 1] as const]]),
      alphas: new Map([[10, 0.25]]),
    });
    expect([...groupColorPlan([10], base, p)!]).toEqual([1, 0, 0, 1]);
  });

  it("restores base colors when a later plan includes everything", () => {
    const out = groupColorPlan(entities, base, plan({ keys: new Set(entities) }))!;
    expect([...out]).toEqual([...base]);
  });

  it("returns null for an empty group", () => {
    expect(groupColorPlan([], base, plan({}))).toBeNull();
  });
});

describe("groupTransformPlan", () => {
  const identity = () => {
    const m = new Float32Array(16);
    m[0] = m[5] = m[10] = m[15] = 1;
    return m;
  };
  const base = new Float32Array(32);
  base.set(identity(), 0);
  base.set(identity(), 16);
  base[12] = 100; // entity 10 base translation x

  const plan = (
    offsets: InstancePlan["offsets"],
  ): InstancePlan => ({ keys: new Set([10, 20]), colors: null, alphas: null, offsets });

  it("returns null when the plan has no offsets", () => {
    expect(groupTransformPlan([10, 20], base, plan(null))).toBeNull();
  });

  it("adds offsets to translation components, keeping base for unplanned entities", () => {
    const out = groupTransformPlan(
      [10, 20],
      base,
      plan(new Map([[10, [1, 2, 3] as const]])),
    )!;
    expect(out[12]).toBe(101);
    expect(out[13]).toBe(2);
    expect(out[14]).toBe(3);
    expect(out[15]).toBe(1); // rest of the matrix untouched
    expect([...out.slice(16)]).toEqual([...base.slice(16)]); // entity 20 unchanged
    expect(base[13]).toBe(0); // input not mutated
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
