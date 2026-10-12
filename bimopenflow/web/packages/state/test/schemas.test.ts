import { describe, expect, it } from "vitest";
import type { PortSchema } from "@bimopenflow/contracts";
import { initialState, reduce, schemaFeeding, schemaMessages, schemaOf, type State } from "../src/index.js";

const port = (node: string, output: string, extra: Partial<PortSchema> = {}): PortSchema => ({
  node, output, status: "Ok", errors: [], warnings: [], ...extra,
});
const withSchemas = (schemas: PortSchema[]): State =>
  reduce(initialState, { type: "applyServerState", update: { analysisId: "x", nodes: [], schemas } });

describe("schemas in State", () => {
  it("starts empty and stays empty when an update carries none", () => {
    expect(initialState.schemas).toEqual({});
    expect(reduce(initialState, { type: "applyServerState", update: { analysisId: "x", nodes: [] } }).schemas).toEqual({});
  });

  it("replaces the list on each update", () => {
    const first = withSchemas([port("a", "out"), port("b", "out")]);
    const second = reduce(first, { type: "applyServerState", update: { analysisId: "x", nodes: [], schemas: [port("b", "out")] } });
    expect(Object.keys(second.schemas)).toEqual(["b.out"]);
  });

  it("keeps the previous list when an update has no schemas field", () => {
    const first = withSchemas([port("a", "out")]);
    const second = reduce(first, { type: "applyServerState", update: { analysisId: "x", nodes: [] } });
    expect(second.schemas).toBe(first.schemas);
  });

  it("is cleared by setDocument", () => {
    const json = JSON.stringify({ structure: { nodes: [], edges: [] }, values: {} });
    expect(reduce(withSchemas([port("a", "out")]), { type: "setDocument", json }).schemas).toEqual({});
  });
});

describe("lookups", () => {
  const base = withSchemas([
    port("a", "out", { columns: [{ name: "Id", type: "Text", nullable: false }] }),
    port("b", "out", { warnings: ["Column 'Nme' is not in the input; nearest is 'Name'."] }),
    port("b", "rest", { warnings: ["Column 'Nme' is not in the input; nearest is 'Name'."], errors: ["bad"] }),
  ]);
  const state: State = {
    ...base,
    document: { ...base.document, structure: { nodes: [], edges: [{ from: "a.out", to: "b.in" }] } },
  };

  it("finds by output endpoint", () => {
    expect(schemaOf(state, "a.out")?.columns?.[0]?.name).toBe("Id");
    expect(schemaOf(state, "a.nope")).toBeUndefined();
  });

  it("finds by the wire feeding an input", () => {
    expect(schemaFeeding(state, "b.in")?.node).toBe("a");
    expect(schemaFeeding(state, "b.other")).toBeUndefined();
  });

  it("collects a node's distinct messages, errors first", () => {
    expect(schemaMessages(state, "b")).toEqual(["bad", "Column 'Nme' is not in the input; nearest is 'Name'."]);
    expect(schemaMessages(state, "a")).toEqual([]);
  });
});
