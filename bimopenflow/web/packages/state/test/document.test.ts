import { describe, expect, it } from "vitest";
import { emptyDocument, parseDocument, parsePortRef, serializeDocument } from "../src/document.js";

const sample = {
  formatVersion: "0.1.0",
  structure: {
    nodes: [
      { id: "b", kind: "sink.table", version: 1 },
      { id: "a", kind: "source.model", version: 2 },
    ],
    edges: [{ from: "a.out", to: "b.in" }],
  },
  values: { a: { path: "model.bos" } },
  layout: { a: { x: 10, y: 20 }, b: { x: 30, y: 40, w: 100, h: 50 } },
};

describe("parseDocument", () => {
  it("parses all four layers", () => {
    const doc = parseDocument(JSON.stringify(sample));
    expect(doc.structure.nodes).toHaveLength(2);
    expect(doc.structure.edges).toEqual([{ from: "a.out", to: "b.in" }]);
    expect(doc.values).toEqual({ a: { path: "model.bos" } });
    expect(doc.layout.b).toEqual({ x: 30, y: 40, w: 100, h: 50 });
  });

  it("defaults missing layout to empty and omits absent session", () => {
    const doc = parseDocument(JSON.stringify({ structure: { nodes: [], edges: [] }, values: {} }));
    expect(doc.layout).toEqual({});
    expect("session" in doc).toBe(false);
  });

  it("rejects a missing structure or values layer", () => {
    expect(() => parseDocument(JSON.stringify({ values: {} }))).toThrow(/structure/);
    expect(() => parseDocument(JSON.stringify({ structure: { nodes: [], edges: [] } }))).toThrow(/values/);
  });

  it("rejects unknown top-level members", () => {
    expect(() => parseDocument(JSON.stringify({ ...sample, extra: 1 }))).toThrow(/extra/);
  });

  it("rejects non-string parameter values", () => {
    const bad = { ...sample, values: { a: { path: 5 } } };
    expect(() => parseDocument(JSON.stringify(bad))).toThrow(/canonical string form/);
  });
});

describe("serializeDocument", () => {
  it("sorts nodes by id, edges by 'to', keys alphabetically, and ends with one LF", () => {
    const text = serializeDocument(parseDocument(JSON.stringify(sample)));
    expect(text.endsWith("}\n")).toBe(true);
    expect(text).not.toContain("\r");
    const round = JSON.parse(text);
    expect(round.structure.nodes.map((n: { id: string }) => n.id)).toEqual(["a", "b"]);
    expect(Object.keys(round)).toEqual(["formatVersion", "layout", "structure", "values"]);
    expect(Object.keys(round.structure.nodes[0])).toEqual(["id", "kind", "version"]);
  });

  it("omits empty layout and session layers", () => {
    const text = serializeDocument(emptyDocument);
    expect(text).not.toContain("layout");
    expect(text).not.toContain("session");
  });

  it("round-trips parse -> serialize -> parse to the same serialization", () => {
    const once = serializeDocument(parseDocument(JSON.stringify(sample)));
    const twice = serializeDocument(parseDocument(once));
    expect(twice).toBe(once);
  });
});

describe("parsePortRef", () => {
  it("splits at the first dot", () => {
    expect(parsePortRef("n1.out.x")).toEqual({ nodeId: "n1", port: "out.x" });
  });

  it("rejects endpoints without both parts", () => {
    for (const bad of ["nodot", ".port", "node.", ""])
      expect(() => parsePortRef(bad)).toThrow(/expected 'nodeId.port'/);
  });
});
