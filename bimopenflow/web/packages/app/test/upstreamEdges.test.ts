import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { upstreamEdges } from "../src/upstreamEdges.js";
import { buildCanvasModel } from "../src/viewModel.js";

const edge = (id: string, from: string, to: string) => ({ id, from, to });

describe("upstreamEdges", () => {
  it("returns nothing for a null selection", () => {
    const model = { edges: [edge("a->b", "a.out", "b.in")] };
    expect(upstreamEdges(model, null)).toEqual(new Set());
  });

  it("returns nothing for a node with no inputs", () => {
    const model = { edges: [edge("a->b", "a.out", "b.in")] };
    expect(upstreamEdges(model, "a")).toEqual(new Set());
  });

  it("walks a cycle-free chain to every source", () => {
    // source -> mid -> sink
    const model = {
      edges: [edge("source->mid", "source.out", "mid.in"), edge("mid->sink", "mid.out", "sink.in")],
    };
    expect(upstreamEdges(model, "sink")).toEqual(new Set(["source->mid", "mid->sink"]));
    expect(upstreamEdges(model, "mid")).toEqual(new Set(["source->mid"]));
  });

  it("includes both branches of a diamond", () => {
    //      -> left  ->
    // top             -> bottom
    //      -> right ->
    const model = {
      edges: [
        edge("top->left", "top.out", "left.in"),
        edge("top->right", "top.out", "right.in"),
        edge("left->bottom", "left.out", "bottom.in"),
        edge("right->bottom", "right.out", "bottom.in"),
      ],
    };
    expect(upstreamEdges(model, "bottom")).toEqual(
      new Set(["top->left", "top->right", "left->bottom", "right->bottom"]),
    );
  });

  it("does not loop forever on a cycle", () => {
    // a <-> b feed c; d also feeds c directly
    const model = {
      edges: [
        edge("a->b", "a.out", "b.in"),
        edge("b->a", "b.out", "a.in"),
        edge("b->c", "b.out", "c.in"),
        edge("d->c", "d.out", "c.in"),
      ],
    };
    const result = upstreamEdges(model, "c");
    expect(result).toEqual(new Set(["a->b", "b->a", "b->c", "d->c"]));
  });
});

const desc2: NodeDescriptor = {
  kind: "k.b",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in1", type: "Table", optional: false }, { name: "in2", type: "Table", optional: false }],
  outputs: [{ name: "out1", type: "Table", optional: false }, { name: "out2", type: "Table", optional: false }],
  params: [],
  description: "",
};

describe("upstreamEdges against a real CanvasModel", () => {
  it("marks only the edges feeding the selected node, not a sibling branch", () => {
    const store = createStore();
    for (const id of ["top", "left", "right", "bottom"])
      store.dispatch({ type: "addNode", id, kind: "k.b", version: 1 });
    store.dispatch({ type: "connect", from: "top.out1", to: "left.in1" });
    store.dispatch({ type: "connect", from: "top.out2", to: "right.in1" });
    store.dispatch({ type: "connect", from: "left.out1", to: "bottom.in1" });
    store.dispatch({ type: "connect", from: "right.out1", to: "bottom.in2" });
    const catalog = new Map([["k.b", desc2]]);

    store.dispatch({ type: "select", ids: ["bottom"] });
    const bottomModel = buildCanvasModel(store.getState(), catalog);
    const selectedFromBottom = bottomModel.nodes.find((n) => n.selected)?.id ?? null;
    expect(upstreamEdges(bottomModel, selectedFromBottom)).toEqual(
      new Set(bottomModel.edges.map((e) => e.id)),
    );

    store.dispatch({ type: "select", ids: ["left"] });
    const leftModel = buildCanvasModel(store.getState(), catalog);
    const selectedFromLeft = leftModel.nodes.find((n) => n.selected)?.id ?? null;
    expect(upstreamEdges(leftModel, selectedFromLeft)).toEqual(
      new Set([leftModel.edges.find((e) => e.from.startsWith("top") && e.to.startsWith("left"))!.id]),
    );

    store.dispatch({ type: "clearSelection" });
    const noneModel = buildCanvasModel(store.getState(), catalog);
    const selectedNone = noneModel.nodes.find((n) => n.selected)?.id ?? null;
    expect(upstreamEdges(noneModel, selectedNone)).toEqual(new Set());
  });
});
