// Headless smoke for the gratify canvas: the parts build, the view expands,
// frames step, and a sync intent swaps the doc — without a real <canvas>.

import { describe, expect, it } from "vitest";
import { Runtime } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";

const desc: NodeDescriptor = {
  kind: "k.a",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table" }],
  outputs: [{ name: "out", type: "Table" }],
  params: [],
  description: "",
};

describe("canvas parts (headless gratify)", () => {
  it("mounts, steps, and syncs a store-built model", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    store.dispatch({ type: "addNode", id: "b", kind: "k.a", version: 1 });
    store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
    store.dispatch({ type: "select", ids: ["b"] });
    const catalog = new Map([["k.a", desc]]);
    const model = buildCanvasModel(store.getState(), catalog);

    const errors: string[] = [];
    const runtime = new Runtime<CanvasModel, CanvasIntent>(
      null,
      {
        init: model,
        update: makeCanvasUpdate(store, (m) => errors.push(m)),
        view: canvasView,
      },
      { headless: true, width: 800, height: 600 },
    );
    runtime.step(3, 1 / 60);

    store.dispatch({ type: "removeNode", id: "b" });
    runtime.dispatch({
      kind: "sync",
      model: buildCanvasModel(store.getState(), catalog),
    });
    runtime.step(3, 1 / 60);

    expect(errors).toEqual([]);
    expect(runtime.doc.nodes.map((n) => n.id)).toEqual(["a"]);
    expect(runtime.doc.edges).toEqual([]);
  });
});
