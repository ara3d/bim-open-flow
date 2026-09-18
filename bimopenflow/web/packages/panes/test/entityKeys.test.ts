import { describe, expect, it } from "vitest";
import { emptyObject, objectKey, objectRef } from "@bim-open-toolkit/model";
import { entityKeyOf, entityKeysByObjectId } from "../src/entityKeys";

const ref = { id: "m", revision: "r" };
const object = (row: number, sourceId?: string) => ({ ...emptyObject(objectRef(ref, `bos:${row}`)), ...(sourceId === undefined ? {} : { sourceId }) });

describe("entityKeyOf", () => {
  it("prefers the source id, which is the STEP express id for IFC-derived BOS", () => {
    expect(entityKeyOf(object(3405, "24813"))).toBe(24813);
  });

  it("falls back to the bos row id when there is no usable source id", () => {
    expect(entityKeyOf(object(7))).toBe(7);
    expect(entityKeyOf(object(8, "not-a-number"))).toBe(8);
  });

  it("is NaN for a missing record", () => {
    expect(entityKeyOf(undefined)).toBeNaN();
  });
});

describe("entityKeysByObjectId", () => {
  it("resolves the object id segment of a pick key to the entity key", () => {
    const objects = [object(0, "514"), object(1)];
    const keys = entityKeysByObjectId(objects);
    const picked = decodeURIComponent(objectKey(objects[0]!.ref).split("|").at(-1)!);
    expect(keys.get(picked)).toBe(514);
    expect(keys.get("bos:1")).toBe(1);
    expect(keys.get("bos:2")).toBeUndefined();
  });
});
