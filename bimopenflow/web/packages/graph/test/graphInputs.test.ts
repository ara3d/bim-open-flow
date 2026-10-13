// @vitest-environment jsdom
// Graph inputs on the canvas (TKT-204): a card per declared input at the top of
// the sources rail with a control for its current value, an edit that goes to
// the host and not to the document, and a parameter holding `$name` shown as a
// chip-styled box titled with the input's value.

import { afterEach, describe, expect, it, vi } from "vitest";
import { rect, type Element, type GNode } from "gratify";
import type { GraphInputInfo } from "@bimopenflow/contracts";
import { createStore, type Store } from "@bimopenflow/state";
import { makeCanvasUpdate, type CanvasIntent } from "../src/canvasIntents.js";
import { slotControl } from "../src/canvasSlots.js";
import { INPUT_KIND } from "../src/graphInputs.js";
import { createCanvasInstance } from "../src/instance.js";
import { slotElement } from "../src/slotRegistry.js";
import { DEFAULT_SOURCE_VIEW, NO_SOURCE_VIEW, type SourceView } from "../src/sources.js";
import { buildCanvasModel, type CanvasNode } from "../src/viewModel.js";
import { catalog } from "./sourceFixtures.js";

afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); });

const MODEL = "C:/m/city.duckdb";

/** A graph with a source `db` holding `$model`, a query `q1` reading the same input, and the input declared. */
function graph(): Store {
  const store = createStore();
  store.dispatch({ type: "declareInput", input: { name: "model", kind: "FilePath", default: MODEL } });
  store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "$model" });
  store.dispatch({ type: "addNode", id: "q1", kind: "duck.query", version: 1 });
  store.dispatch({ type: "setLayout", nodeId: "q1", layout: { x: 400, y: 100 } });
  store.dispatch({ type: "connect", from: "db.source", to: "q1.path" });
  return store;
}
const withValue = (store: Store, value: string): void => {
  const info: GraphInputInfo = { name: "model", kind: "FilePath", default: MODEL, description: "", value };
  store.dispatch({ type: "applyServerState", update: { analysisId: "a", nodes: [], graphInputs: [info] } });
};
const build = (store: Store, sources: SourceView = DEFAULT_SOURCE_VIEW) =>
  buildCanvasModel(store.getState(), catalog, null, undefined, { sources });
const card = (store: Store, id = "$model", sources?: SourceView): CanvasNode =>
  build(store, sources).nodes.find((n) => n.id === id)!;

describe("the graph input card", () => {
  it("is the first card of the rail, left of the flow, marked as an input", () => {
    const model = build(graph());
    const input = model.nodes.find((n) => n.id === "$model")!;
    const db = model.nodes.find((n) => n.id === "db")!;
    expect(model.nodes[0]).toBe(input);
    expect(input).toMatchObject({ kind: INPUT_KIND, inRail: true, input: { name: "model", kind: "FilePath" }, description: "Graph input", selected: false });
    // db is wired to q1, so it sits in the flow (TKT-286); the rail, holding the input, stands left of it.
    expect(db.inRail).toBeUndefined();
    expect(input.x + input.w).toBeLessThan(db.x);
  });

  it("holds one row named after the input: the declared default, then the host's current value", () => {
    const store = graph();
    expect(card(store).params).toMatchObject([{ name: "model", kind: "FilePath", value: MODEL }]);
    withValue(store, "C:/m/other.duckdb");
    expect(card(store).params[0]!.value).toBe("C:/m/other.duckdb");
  });

  it("stays in the rail when the rail is off for sources, and a graph with no inputs has none", () => {
    expect(card(graph(), "$model", NO_SOURCE_VIEW).inRail).toBe(true);
    expect(card(graph(), "db", NO_SOURCE_VIEW).inRail).toBeUndefined();
    const plain = createStore();
    plain.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    expect(build(plain).nodes.some((n) => n.input)).toBe(false);
  });

  it("takes the file suggestions of a parameter that uses the input", () => {
    const param = card(graph()).params[0]!;
    expect(param.suggest).toEqual({ kind: "FilesUnderRoots", source: ".duckdb" });
    expect(slotControl(param)).toBe("filePath");
  });
});

/** The native input of a row, as the canvas would mount it. */
function mount(store: Store, nodeId: string, param: string, instance = createCanvasInstance({ document })) {
  const node = build(store).nodes.find((n) => n.id === nodeId)!;
  const p = node.params.find((x) => x.name === param)!;
  const element: Element = slotElement({ nodeId, param: p, instance, w: node.w - 20, open: false });
  const gnode = { key: element.key, props: element.props, rect: rect(0, 0, 240, 50), ch: {}, states: new Set(), local: element.part.localInit } as GNode<unknown>;
  const el = element.part.island!(gnode)!.el as HTMLElement;
  return { instance, el, input: (el instanceof HTMLInputElement ? el : el.querySelector("input"))! };
}

describe("editing a graph input's value", () => {
  it("shows the FilePath control, with a browse button when the host serves files", () => {
    const instance = createCanvasInstance({
      document,
      suggestionProvider: () => Promise.resolve({ status: "Ok", values: [] }),
      fileProvider: { browse: () => Promise.reject(new Error("unused")), stat: () => Promise.resolve({ path: MODEL, exists: true }) },
    });
    const { el, input } = mount(graph(), "$model", "model", instance);
    expect(input.getAttribute("aria-label")).toBe("$model model");
    expect(el.querySelector('button[aria-label="$model model browse"]')).not.toBeNull();
  });

  it("sends the new value to the host through the hook and leaves the document alone", () => {
    const store = graph();
    const sent: [string, string][] = [];
    const update = makeCanvasUpdate(store, (m) => { throw new Error(m); }, () => null, { onSetInput: (n, v) => sent.push([n, v]) });
    const doc = build(store);
    const before = store.getState().document;
    update(doc, { kind: "setParam", nodeId: "$model", name: "model", value: "C:/m/other.duckdb" });
    expect(sent).toEqual([["model", "C:/m/other.duckdb"]]);
    expect(store.getState().document).toBe(before);
    expect(store.getState().selection).toEqual([]);
  });

  it("is what typing in the card's box does", () => {
    const store = graph();
    const { instance, input } = mount(store, "$model", "model");
    const intents: CanvasIntent[] = [];
    instance.dispatch = (i) => intents.push(i);
    input.value = "C:/m/other.duckdb";
    input.dispatchEvent(new Event("change"));
    expect(intents).toEqual([{ kind: "setParam", nodeId: "$model", name: "model", value: "C:/m/other.duckdb" }]);
  });

  it("is dropped by a viewer, and pressing the card selects nothing", () => {
    const store = graph();
    const sent: string[] = [];
    const doc = build(store);
    const viewer = makeCanvasUpdate(store, () => {}, () => null, { onSetInput: (_n, v) => sent.push(v) }, true);
    viewer(doc, { kind: "setParam", nodeId: "$model", name: "model", value: "x" });
    expect(sent).toEqual([]);
    const editor = makeCanvasUpdate(store, () => {}, () => null, { onSetInput: (_n, v) => sent.push(v) });
    editor(doc, { kind: "selectNode", id: "$model" });
    expect(store.getState().selection).toEqual([]);
    editor(doc, { kind: "selectNode", id: "db" });
    expect(store.getState().selection).toEqual(["db"]);
  });
});

describe("a parameter holding $name", () => {
  it("carries the input's name and current value, and is a text box whatever its kind", () => {
    const store = graph();
    withValue(store, "C:/m/other.duckdb");
    const param = card(store, "db").params[0]!;
    expect(param.inputRef).toEqual({ name: "model", value: "C:/m/other.duckdb" });
    expect(slotControl(param)).toBe("field");
  });

  it("shows the reference in a chip-styled box titled with the resolved value", () => {
    const store = graph();
    withValue(store, "C:/m/other.duckdb");
    const { input } = mount(store, "db", "path");
    expect(input.value).toBe("$model");
    expect(input.title).toBe("Graph input model: C:/m/other.duckdb");
    expect(input.style.borderRadius).toBe("10px");
  });

  it("is plain text for a numeric parameter, so $n is not rejected as a number", () => {
    const store = createStore();
    store.dispatch({ type: "declareInput", input: { name: "n", kind: "Integer", default: "3" } });
    store.dispatch({ type: "addNode", id: "s", kind: "num.like", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "s", name: "count", value: "$n" });
    const numeric = { ...catalog.get("duck.source")!, kind: "num.like", params: [{ name: "count", kind: "Integer" as const, default: "1" }] };
    const model = buildCanvasModel(store.getState(), new Map([["num.like", numeric]]), null, undefined, { sources: NO_SOURCE_VIEW });
    const param = model.nodes.find((n) => n.id === "s")!.params[0]!;
    expect(param.inputRef).toEqual({ name: "n", value: "3" });
    expect(slotControl(param)).toBe("field");
  });

  it("typing a literal in its place sets that one parameter and leaves the input declared", () => {
    const store = graph();
    const { instance, input } = mount(store, "db", "path");
    const intents: CanvasIntent[] = [];
    instance.dispatch = (i) => intents.push(i);
    input.value = "C:/m/mine.duckdb";
    input.dispatchEvent(new Event("change"));
    expect(intents).toEqual([{ kind: "setParam", nodeId: "db", name: "path", value: "C:/m/mine.duckdb" }]);
    const update = makeCanvasUpdate(store, (m) => { throw new Error(m); });
    update(build(store), intents[0]!);
    expect(store.getState().document.values["db"]!["path"]).toBe("C:/m/mine.duckdb");
    expect(store.getState().document.structure.inputs).toHaveLength(1);
    expect(card(store, "db").params[0]!.inputRef).toBeUndefined();
  });

  it("is not marked for an undeclared name, an escaped $$, or a parameter an edge feeds", () => {
    const store = graph();
    store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "$nothing" });
    expect(card(store, "db").params[0]!.inputRef).toBeUndefined();
    store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "$$model" });
    expect(card(store, "db").params[0]!.inputRef).toBeUndefined();
    store.dispatch({ type: "setParam", nodeId: "q1", name: "path", value: "$model" });
    expect(card(store, "q1").params[0]!.inputRef).toBeUndefined();
  });
});
