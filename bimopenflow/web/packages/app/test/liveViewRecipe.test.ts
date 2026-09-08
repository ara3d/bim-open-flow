import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import { buildLiveViewRecipe } from "../src/liveViewRecipe";

const defaults: Record<string, Record<string, string>> = {
  scene: { path: "Snowdon.bos" }, section: { axis: "z", fraction: "0.5" },
  sectionBox: { fraction: "0.5" }, explode: { strength: "0.5" },
  projection: { mode: "orthographic" }, environment: { theme: "light", grid: "true" },
  categoryStyle: { opacity: "1" }, tint: { color: "#2b8bd6", opacityPercent: "100" },
  sectionRange: { axis: "z", range: "[0.3,0.7]" },
};
const catalog = new Map<string, NodeDescriptor>(Object.entries(defaults).map(([operation, parameters]) => {
  const kind = `view3d.${operation}`;
  return [kind, { kind, version: 1, capability: "Pure", inputs: [], outputs: [], description: "",
    params: Object.entries(parameters).map(([name, value]) => ({ name, kind: "Text", default: value })) }];
}));
function graph(...operations: string[]): GraphDocument {
  return {
    formatVersion: "0.1.0", values: {}, layout: {},
    structure: {
      nodes: operations.map((operation, i) => ({ id: `n${i}`, kind: `view3d.${operation}`, version: 1 })),
      edges: operations.slice(1).map((_, i) => ({ from: `n${i}.view`, to: `n${i + 1}.view` })),
    },
  };
}
function steps(document: GraphDocument, nodeId = document.structure.nodes[document.structure.nodes.length - 1].id) {
  const result = buildLiveViewRecipe(document, nodeId, catalog);
  expect(result.kind).toBe("ready");
  if (result.kind !== "ready") throw new Error(JSON.stringify(result));
  expect(result.data.totalRows).toBe(result.data.rows.length);
  return result.data.rows.map(([operation, input]) => ({ operation, input: JSON.parse(String(input)) }));
}

describe("live view recipes", () => {
  it("uses current explode slider values immediately and leaves the graph unchanged", () => {
    const original = graph("scene", "explode");
    const changed = { ...original, values: { n1: { strength: "2.35" } } };
    expect(steps(changed)).toEqual([
      { operation: "scene", input: { path: "Snowdon.bos" } },
      { operation: "explode", input: { by: "category", strength: 2.35 } },
    ]);
    expect(steps(original)[1].input.strength).toBe(.5);
    expect(changed.values.n1.strength).toBe("2.35");
  });
  it("composes only connected ancestors up to the selected node", () => {
    const doc = graph("scene", "categoryStyle", "section", "projection");
    expect(steps(doc, "n2").map(step => step.operation)).toEqual(["scene", "categoryStyle", "section"]);
    expect(steps({ ...doc, structure: { ...doc.structure, nodes: [...doc.structure.nodes, { id: "unrelated", kind: "table.read", version: 1 }] } }, "n2"))
      .toEqual(steps(doc, "n2"));
  });
  it("converts Percent to Fraction once and leaves normalized Fraction values unchanged", () => {
    const doc = graph("scene", "categoryStyle", "tint", "section");
    const result = steps({ ...doc, values: { n1: { opacity: ".25" }, n2: { opacityPercent: "35" }, n3: { fraction: ".8" } } });
    expect(result[1].input).toEqual({ opacity: .25 });
    expect(result[2].input).toEqual({ color: "#2b8bd6", opacity: .35 });
    expect(result[3].input).toEqual({ axis: "z", fraction: .8 });
  });
  it("matches the range, environment, section box and projection payloads", () => {
    const doc = graph("scene", "sectionRange", "environment", "sectionBox", "projection");
    expect(steps({ ...doc, values: { n1: { range: "[0.1,0.9]" }, n2: { theme: "dark", grid: "False" }, n4: { mode: "plan" } } }).slice(1))
      .toEqual([
        { operation: "sectionRange", input: { axis: "z", range: [.1, .9] } },
        { operation: "environment", input: { theme: "dark", grid: false } },
        { operation: "sectionBox", input: { fraction: .5 } },
        { operation: "projection", input: { mode: "plan" } },
      ]);
  });
  it("rejects disconnected, missing, duplicate and cyclic inputs", () => {
    const doc = graph("scene", "explode");
    for (const edges of [[], [{ from: "missing.view", to: "n1.view" }],
      [...doc.structure.edges, ...doc.structure.edges], [{ from: "n1.view", to: "n1.view" }]]) {
      expect(buildLiveViewRecipe({ ...doc, structure: { ...doc.structure, edges } }, "n1", catalog).kind).toBe("invalid");
    }
    expect(buildLiveViewRecipe(doc, "missing", catalog).kind).toBe("invalid");
  });
  it("rejects input connections to a source and limits chains to 64 steps", () => {
    const doc = graph("scene");
    expect(buildLiveViewRecipe({ ...doc, structure: { ...doc.structure, edges: [{ from: "n0.view", to: "n0.view" }] } }, "n0", catalog).kind).toBe("invalid");
    expect(steps(graph("scene", ...Array<string>(63).fill("explode")))).toHaveLength(64);
    expect(buildLiveViewRecipe(graph("scene", ...Array<string>(64).fill("explode")), "n64", catalog).kind).toBe("invalid");
  });
  it("defers mixed graphs and unknown node versions to host evaluation", () => {
    expect(buildLiveViewRecipe(graph("scene", "arbitraryCommand"), "n1", catalog).kind).toBe("unsupported");
    const doc = graph("scene", "explode");
    expect(buildLiveViewRecipe({ ...doc, structure: { ...doc.structure, nodes: [{ id: "n0", kind: "table.read", version: 1 }, doc.structure.nodes[1]] } }, "n1", catalog).kind).toBe("unsupported");
    expect(buildLiveViewRecipe({ ...doc, structure: { ...doc.structure, edges: [{ from: "n0.table", to: "n1.view" }] } }, "n1", catalog).kind).toBe("unsupported");
    expect(buildLiveViewRecipe(doc, "n1", new Map()).kind).toBe("unsupported");
  });
  it.each([
    ["explode", "strength", "5.1"], ["explode", "strength", "NaN"], ["explode", "strength", ""], ["explode", "strength", "0xff"],
    ["section", "fraction", "1.01"], ["sectionBox", "fraction", "0"], ["categoryStyle", "opacity", "-0.1"],
    ["tint", "opacityPercent", "101"], ["tint", "color", "red"], ["sectionRange", "range", "[0.9,0.2]"],
    ["sectionRange", "range", "oops"], ["projection", "mode", "fisheye"], ["environment", "grid", "1"],
  ])("rejects illegal %s.%s = %s without returning a successful recipe", (operation, parameter, value) => {
    const doc = graph("scene", operation);
    expect(buildLiveViewRecipe({ ...doc, values: { n1: { [parameter]: value } } }, "n1", catalog).kind).toBe("invalid");
  });
});
