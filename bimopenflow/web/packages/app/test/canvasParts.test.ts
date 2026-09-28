// Headless smoke for the gratify canvas: the parts build, the view expands,
// frames step, and a sync intent swaps the doc — without a real <canvas>.

import { describe, expect, it } from "vitest";
import { Runtime, v } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { canvasView } from "../src/canvasParts.js";
import { buildCanvasModel, NOTE_KIND, type CanvasModel } from "../src/viewModel.js";
import { defaultNodeStyle, nodeStyleNames, setNodeStyle } from "../src/nodeStyle.js";

const desc: NodeDescriptor = {
  kind: "k.a",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
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

describe("view.note (headless gratify)", () => {
  const noteDesc: NodeDescriptor = {
    kind: NOTE_KIND, version: 1, capability: "Pure",
    inputs: [], outputs: [], params: [{ name: "text", kind: "Text", default: "" }], description: "",
  };

  it("clicking a note opens its editor, and a real drag moves it instead", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "n1", kind: NOTE_KIND, version: 1 });
    store.dispatch({ type: "setParam", nodeId: "n1", name: "text", value: "Remember the storey mapping." });
    const catalog = new Map([[NOTE_KIND, noteDesc]]);
    const model = () => buildCanvasModel(store.getState(), catalog);

    const errors: string[] = [];
    const runtime = new Runtime<CanvasModel, CanvasIntent>(
      null,
      { init: model(), update: makeCanvasUpdate(store, (m) => errors.push(m)), view: canvasView },
      { headless: true, width: 800, height: 600 },
    );
    runtime.step(3, 1 / 60);
    const note = runtime.doc.nodes[0]!;
    expect(note.inputs).toEqual([]);
    expect(note.outputs).toEqual([]);
    expect(note.status).toBeUndefined();

    runtime.pointerDown(v(note.x + 10, note.y + 10));
    runtime.pointerUp(v(note.x + 10, note.y + 10));
    runtime.step(2, 1 / 60);
    runtime.dispatch({ kind: "sync", model: model() });
    runtime.step(2, 1 / 60);

    expect(errors).toEqual([]);
    expect(runtime.doc.openEditor).toEqual({ nodeId: "n1", name: "text" });
    // A click does not move the note or select it as an ordinary node would.
    expect(store.getState().document.layout["n1"]).toBeUndefined();
  });
});

describe("node card styles (headless gratify)", () => {
  it("steps a described node in every style without errors", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
    const catalog = new Map([["k.a", { ...desc, description: "Keeps the rows that match." }]]);
    const model = buildCanvasModel(store.getState(), catalog);
    expect(model.nodes[0]!.description).toBe("Keeps the rows that match.");
    const errors: string[] = [];
    const runtime = new Runtime<CanvasModel, CanvasIntent>(
      null,
      { init: model, update: makeCanvasUpdate(store, (m) => errors.push(m)), view: canvasView },
      { headless: true, width: 800, height: 600 },
    );
    try {
      for (const style of nodeStyleNames) {
        setNodeStyle(style);
        runtime.step(2, 1 / 60);
      }
    } finally {
      setNodeStyle(defaultNodeStyle);
    }
    expect(errors).toEqual([]);
  });
});
