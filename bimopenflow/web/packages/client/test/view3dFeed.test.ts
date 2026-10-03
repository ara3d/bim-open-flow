import type { TableSlice } from "@bimopenflow/contracts";
import { describe, expect, it } from "vitest";
import { hostMessage } from "../src/hostMessage";
import { modelUrlFor, view3dDataKind } from "../src/view3dFeed";

const table = (...names: string[]): TableSlice => ({
  columns: names.map((name) => ({ name, type: "Integer" as const })),
  rows: [],
  totalRows: 0,
  skip: 0,
});

describe("view3dDataKind", () => {
  it("takes the port name first", () => {
    expect(view3dDataKind("view", table("entityId"))).toBe("view");
    expect(view3dDataKind("boxes", table("entityId"))).toBe("boxes");
  });

  it("reads a bounds table on any other port as boxes, else instances", () => {
    expect(view3dDataKind("out", table("minX", "minY", "minZ", "maxX", "maxY", "maxZ"))).toBe("boxes");
    expect(view3dDataKind("out", table("entityId", "instanceIndex"))).toBe("instances");
  });
});

describe("modelUrlFor", () => {
  it("names a catalog model by id", () => {
    expect(modelUrlFor("a/b.bos", "b")).toBe("model:b");
  });

  it("says how to fix a model the catalog does not hold", () => {
    expect(() => modelUrlFor("a/b.bos", null)).toThrow(
      "Model is not in the host catalog: a/b.bos. Add its directory to ModelRoots.",
    );
  });
});

describe("hostMessage", () => {
  it("returns the host's error sentence from an ApiClient message", () => {
    expect(hostMessage(new Error('GET /x -> 404: {"error":"No node n1"}'))).toBe("No node n1");
  });

  it("returns the whole message when it carries no JSON error", () => {
    expect(hostMessage(new Error("GET /x -> 500: {not json"))).toBe("GET /x -> 500: {not json");
    expect(hostMessage("plain")).toBe("plain");
  });
});
