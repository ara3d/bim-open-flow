import { describe, expect, it } from "vitest";
import type { ModelSummary } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import { matchModelId, modelPathFor } from "../src/modelRef.js";

const doc = (
  nodes: { id: string; path?: string }[],
  edges: [string, string][] = [],
): GraphDocument => ({
  formatVersion: "0.1.0",
  structure: {
    nodes: nodes.map((n) => ({ id: n.id, kind: "k", version: 1 })),
    edges: edges.map(([from, to]) => ({ from, to })),
  },
  values: Object.fromEntries(
    nodes.filter((n) => n.path !== undefined).map((n) => [n.id, { path: n.path! }]),
  ),
  layout: {},
});

describe("modelPathFor", () => {
  it("returns the node's own path param", () => {
    expect(modelPathFor(doc([{ id: "a", path: "x.bos" }]), "a")).toBe("x.bos");
  });

  it("walks upstream to the nearest node with a path", () => {
    const d = doc(
      [{ id: "load", path: "m.ifc" }, { id: "iso" }, { id: "color" }],
      [["load.instances", "iso.table"], ["iso.table", "color.table"]],
    );
    expect(modelPathFor(d, "color")).toBe("m.ifc");
  });

  it("prefers the nearer path over a farther one", () => {
    const d = doc(
      [{ id: "far", path: "far.bos" }, { id: "near", path: "near.bos" }, { id: "sink" }],
      [["far.out", "near.in"], ["near.out", "sink.in"]],
    );
    expect(modelPathFor(d, "sink")).toBe("near.bos");
  });

  it("ignores blank paths and survives cycles", () => {
    const d = doc(
      [{ id: "a", path: "  " }, { id: "b" }],
      [["a.out", "b.in"], ["b.out", "a.in"]],
    );
    expect(modelPathFor(d, "b")).toBeUndefined();
  });
});

const model = (id: string, sourcePath: string): ModelSummary => ({
  id,
  name: id,
  kind: "Bos",
  sizeBytes: 1,
  lastWriteUtc: "2026-01-01T00:00:00.000Z",
  sourcePath,
});

describe("matchModelId", () => {
  const models = [
    model("duplex.ifc", "C:\\repo\\data\\duplex.ifc"),
    model("sample.bos", "C:\\repo\\samples\\bim\\sample.bos"),
  ];

  it("matches an absolute path exactly, ignoring separators and case", () => {
    expect(matchModelId(models, "C:/repo/samples/bim/Sample.BOS")).toBe("sample.bos");
  });

  it("matches a relative path as a unique suffix", () => {
    expect(matchModelId(models, "data/duplex.ifc")).toBe("duplex.ifc");
  });

  it("returns undefined for an ambiguous or unknown path", () => {
    expect(matchModelId(models, "nothing.ifc")).toBeUndefined();
    const twins = [model("a", "C:\\x\\m.bos"), model("b", "C:\\y\\m.bos")];
    expect(matchModelId(twins, "m.bos")).toBeUndefined();
  });
});
