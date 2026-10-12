// peekCard.ts: the pure grid and row-count text, and a headless-gratify smoke
// for the adornment (design.md worked examples, chunk C6's test list).

import { describe, expect, it } from "vitest";
import { part, Runtime, v, withExt, type Element } from "gratify";
import type { PortSchema, TableSlice } from "@bimopenflow/contracts";
import { noteLines, peekAdorn, peekGrid, rowCountText } from "../src/peekCard.js";
import type { PortPeekView } from "../src/portResults.js";

const readySlice: TableSlice = {
  columns: [
    { name: "id", type: "Integer" },
    { name: "height", type: "Number" },
  ],
  rows: [
    [2, 3.0],
    [3, 4.5],
  ],
  totalRows: 2,
  skip: 0,
};

const readyView: PortPeekView = {
  endpoint: "tall.relation",
  pinned: false,
  peek: { kind: "ready", slice: readySlice },
};

describe("peekGrid", () => {
  it("matches the worked example for a ready peek", () => {
    expect(peekGrid(readyView)).toEqual({
      title: "tall.relation · 2 rows · 2 columns",
      columns: ["id", "height"],
      rows: [
        ["2", "3"],
        ["3", "4.5"],
      ],
      note: null,
    });
  });

  it("shows a loading note with no columns or rows", () => {
    const view: PortPeekView = { endpoint: "tall.relation", pinned: false, peek: { kind: "loading" } };
    expect(peekGrid(view)).toEqual({
      title: "tall.relation",
      columns: [],
      rows: [],
      note: "loading…",
    });
  });

  it("shows the absent reason as the note", () => {
    const view: PortPeekView = {
      endpoint: "out.out",
      pinned: false,
      peek: { kind: "absent", reason: "Writes on Run; no rows until the graph runs" },
    };
    expect(peekGrid(view)).toEqual({
      title: "out.out",
      columns: [],
      rows: [],
      note: "Writes on Run; no rows until the graph runs",
    });
  });

  it("folds columns past PEEK_COLUMNS into the note, and null cells read 'null'", () => {
    const slice: TableSlice = {
      columns: ["a", "b", "c", "d", "e", "f", "g"].map((name) => ({ name, type: "Text" as const })),
      rows: [["a", null, "c", "d", "e", "f", "g"]],
      totalRows: 1,
      skip: 0,
    };
    const view: PortPeekView = { endpoint: "n.out", pinned: false, peek: { kind: "ready", slice } };
    const grid = peekGrid(view);
    expect(grid.columns).toEqual(["a", "b", "c", "d", "e", "f"]);
    expect(grid.rows).toEqual([["a", "null", "c", "d", "e", "f"]]);
    expect(grid.note).toBe("+1 more columns");
  });
});

describe("rowCountText", () => {
  it("matches the worked examples", () => {
    expect(rowCountText(0)).toBe("0 rows");
    expect(rowCountText(1)).toBe("1 row");
    expect(rowCountText(456598)).toBe("456,598 rows");
  });
});

// A minimal stand-in for canvasParts.ts's GraphNodePart: just enough shape
// (id, outputs, a fixed size) for peekAdorn to place a card beside a socket.
// canvasParts.ts wires peekAdorn onto the real part in C7; this module must
// not import it (outside this chunk's fence).
interface TestNodeProps {
  readonly id: string;
  readonly outputs: readonly { readonly name: string }[];
}

const TestNode = part<TestNodeProps>()("test-peek-node", {
  size: () => v(184, 90),
  render() {
    // no drawing needed for the smoke test
  },
});

function rootView(peek: PortPeekView | undefined): Element {
  return withExt(TestNode("tall", { id: "tall", outputs: [{ name: "relation" }] }), peekAdorn(peek));
}

describe("peekAdorn", () => {
  it("adds no elements for a node other than the peeked one", () => {
    const otherView = peekAdorn(readyView);
    const fakeNode = { props: { id: "other", outputs: [{ name: "relation" }] } } as any;
    // peekAdorn wraps addAdorn; drive its `adorn` facet directly on a node
    // whose id does not match the view's endpoint.
    const def = otherView({ adorn: undefined } as any);
    expect(def.adorn!(fakeNode)).toEqual([]);
  });

  it("adds no elements when the view is undefined", () => {
    const ext = peekAdorn(undefined);
    const def = ext({ adorn: undefined } as any);
    const fakeNode = { props: { id: "tall", outputs: [{ name: "relation" }] } } as any;
    expect(def.adorn!(fakeNode)).toEqual([]);
  });

  it("draws the card for the peeked node without errors (headless gratify Runtime)", () => {
    const errors: string[] = [];
    const runtime = new Runtime<PortPeekView | undefined, never>(
      null,
      {
        init: readyView,
        update: (doc) => doc,
        view: rootView,
      },
      { headless: true, width: 800, height: 600 },
    );
    try {
      runtime.step(3, 1 / 60);
    } catch (err) {
      errors.push(String(err));
    }
    expect(errors).toEqual([]);
  });
});

describe("peekGrid for an inferred schema", () => {
  const schema = (extra: Partial<PortSchema>): PortSchema => ({
    node: "n", output: "out", status: "Ok", errors: [], warnings: [], ...extra,
  });
  const inferred = (s: PortSchema, reason = "Not evaluated yet"): PortPeekView => ({
    endpoint: "n.out", pinned: false, peek: { kind: "inferred", schema: s, reason },
  });

  it("Ok: column names, then types (? for nullable), then kinds when any column has one", () => {
    const grid = peekGrid(inferred(schema({
      columns: [
        { name: "Id", type: "Text", nullable: false },
        { name: "Height", type: "Number", nullable: true, kind: "length" },
      ],
    })));
    expect(grid).toEqual({
      title: "n.out · inferred · 2 columns",
      columns: ["Id", "Height"],
      rows: [["Text", "Number?"], ["", "length"]],
      note: "Not evaluated yet",
    });
  });

  it("Ok: omits the kind row when no column has a kind, and folds extra columns into the note", () => {
    const names = ["a", "b", "c", "d", "e", "f", "g"];
    const grid = peekGrid(inferred(schema({ columns: names.map((name) => ({ name, type: "Integer" as const, nullable: false })) })));
    expect(grid.rows).toHaveLength(1);
    expect(grid.note).toBe("+1 more columns · Not evaluated yet");
  });

  it("Dynamic: says the columns are known after Run, with the reason", () => {
    const grid = peekGrid(inferred(schema({ status: "Dynamic", reason: "pivot columns come from the data" })));
    expect(grid.note).toBe("Columns known after Run: pivot columns come from the data");
    expect(grid.columns).toEqual([]);
  });

  it("Unknown: names the reason, which names the node that made it unknown", () => {
    const grid = peekGrid(inferred(schema({ status: "Unknown", reason: "src reads a file that is not set" })));
    expect(grid.note).toBe("Columns unknown: src reads a file that is not set");
  });

  it("Error: shows the first error", () => {
    const grid = peekGrid(inferred(schema({ status: "Error", errors: ["no column Foo"] })));
    expect(grid.note).toBe("Columns unavailable: no column Foo");
  });
});

describe("peekGrid for a result that differs from the inferred columns", () => {
  const inferredSchema = (names: string[]): PortSchema => ({
    node: "tall", output: "relation", status: "Ok", errors: [], warnings: [],
    columns: names.map((name) => ({ name, type: "Integer" as const, nullable: false })),
  });

  it("adds no marker when the names match", () => {
    const view: PortPeekView = { ...readyView, peek: { kind: "ready", slice: readySlice, inferred: inferredSchema(["id", "height"]) } };
    expect(peekGrid(view).note).toBeNull();
  });

  it("flags columns not produced and columns not inferred", () => {
    const view: PortPeekView = { ...readyView, peek: { kind: "ready", slice: readySlice, inferred: inferredSchema(["id", "width"]) } };
    expect(peekGrid(view).note).toBe("differs from inferred columns (not produced: width; not inferred: height)");
  });

  it("ignores an inferred schema that is not Ok", () => {
    const schema: PortSchema = { node: "tall", output: "relation", status: "Dynamic", errors: [], warnings: [] };
    const view: PortPeekView = { ...readyView, peek: { kind: "ready", slice: readySlice, inferred: schema } };
    expect(peekGrid(view).note).toBeNull();
  });
});

describe("noteLines", () => {
  it("breaks at spaces and keeps a long word whole", () => {
    expect(noteLines(null)).toEqual([]);
    expect(noteLines("short")).toEqual(["short"]);
    const lines = noteLines("Columns known after Run: pivot columns come from the data values");
    expect(lines.every((l) => l.length <= 44)).toBe(true);
    expect(lines.join(" ")).toBe("Columns known after Run: pivot columns come from the data values");
  });
});
