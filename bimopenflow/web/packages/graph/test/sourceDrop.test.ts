// @vitest-environment jsdom
// Drop and match (TKT-138): a model file dropped on the canvas becomes a source;
// a path box holding a source's path offers "use <source>".

import { afterEach, describe, expect, it, vi } from "vitest";
import { createStore } from "@bimopenflow/state";
import { anchorId } from "../src/canvasIntents.js";
import { installFileDrop } from "../src/fileDrop.js";
import { FilePathControls } from "../src/filePathControl.js";
import { createCanvasInstance } from "../src/instance.js";
import { dropSourcePlan, dropSourceType } from "../src/sourcePlans.js";
import { installSourceGestures } from "../src/sourceGestures.js";
import { findSourceByPath } from "../src/sources.js";
import { buildCanvasModel } from "../src/viewModel.js";
import { catalog, queryGraph } from "./sourceFixtures.js";

afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); });

const at = { x: 40, y: 60 };

describe("dropSourcePlan", () => {
  it("makes a duck.source from a .duckdb file, a value.path from a .bos or .ifc file", () => {
    expect(["a.duckdb", "B.IFC", "c.bos", "d.txt", "C:\\m\\e.DuckDB"].map(dropSourceType))
      .toEqual(["Database", "Text", "Text", undefined, "Database"]);
    const state = createStore().getState();
    const duck = dropSourcePlan(state, catalog, "C:/m/city.duckdb", at);
    expect(duck).toMatchObject({ source: { id: "source1", endpoint: "source1.source" } });
    expect("actions" in duck && duck.actions).toEqual([
      { type: "addNode", id: "source1", kind: "duck.source", version: 1 },
      { type: "setParam", nodeId: "source1", name: "path", value: "C:/m/city.duckdb" },
      { type: "setLayout", nodeId: "source1", layout: at },
      { type: "select", ids: ["source1"] },
    ]);
    const ifc = dropSourcePlan(state, catalog, "C:/m/house.ifc", at);
    expect("actions" in ifc && ifc.actions[0]).toEqual({ type: "addNode", id: "path1", kind: "value.path", version: 1 });
  });

  it("refuses another kind of file, naming it", () => {
    expect(dropSourcePlan(createStore().getState(), catalog, "C:/m/notes.txt", at))
      .toEqual({ error: "notes.txt is not a .duckdb, .bos or .ifc file" });
  });

  it("selects the source that already holds the path instead of adding a second", () => {
    const store = createStore();
    const first = dropSourcePlan(store.getState(), catalog, "C:/m/city.duckdb", at);
    store.dispatch({ type: "batch", actions: "actions" in first ? first.actions : [] });
    const again = dropSourcePlan(store.getState(), catalog, "C:\\m\\city.duckdb", at);
    expect(again).toMatchObject({ actions: [{ type: "select", ids: ["source1"] }] });
  });
});

describe("installFileDrop", () => {
  const drop = (canvas: HTMLCanvasElement, files: object[], types = ["Files"], type = "drop") => {
    const event = new Event(type, { bubbles: true, cancelable: true });
    Object.assign(event, { dataTransfer: { types, files, dropEffect: "none" }, clientX: 7, clientY: 9 });
    canvas.dispatchEvent(event);
    return event;
  };

  it("reports each dropped file with its full path when the shell gives one, else its name", () => {
    const canvas = document.createElement("canvas");
    const onFile = vi.fn();
    const dispose = installFileDrop(canvas, { onFile });
    expect(drop(canvas, [], ["Files"], "dragover").defaultPrevented).toBe(true);
    drop(canvas, [{ name: "city.duckdb", path: "C:/m/city.duckdb" }, { name: "house.ifc" }]);
    expect(onFile.mock.calls).toEqual([["C:/m/city.duckdb", 7, 9], ["house.ifc", 7, 9]]);
    expect(drop(canvas, [{ name: "x.ifc" }], ["text/plain"]).defaultPrevented).toBe(false);
    expect(onFile).toHaveBeenCalledTimes(2);
    dispose();
    drop(canvas, [{ name: "y.ifc" }]);
    expect(onFile).toHaveBeenCalledTimes(2);
  });
});

describe("a file dropped on the canvas", () => {
  function mountGestures(readOnly = false) {
    const store = createStore();
    const canvas = document.createElement("canvas");
    document.body.append(canvas);
    const errors: string[] = [];
    const gestures = installSourceGestures(canvas, {
      store, getCatalog: () => catalog, onError: (m) => errors.push(m), readOnly,
      getDoc: () => buildCanvasModel(store.getState(), catalog),
      canvasToWorld: (x, y) => ({ x, y }), clientToWorld: (x, y) => ({ x: x * 2, y: y * 2 }),
      getView: () => ({ rail: true, chips: true }), setView: () => {}, onLit: () => {},
    });
    const drop = (file: object) => {
      const event = new Event("drop", { bubbles: true, cancelable: true });
      Object.assign(event, { dataTransfer: { types: ["Files"], files: [file] }, clientX: 10, clientY: 20 });
      canvas.dispatchEvent(event);
    };
    return { store, errors, drop, gestures };
  }

  it("adds a source holding its path in one undo step", () => {
    const { store, drop, errors } = mountGestures();
    drop({ name: "city.duckdb", path: "C:/m/city.duckdb" });
    expect(errors).toEqual([]);
    const { document: doc } = store.getState();
    expect(doc.structure.nodes).toEqual([{ id: "source1", kind: "duck.source", version: 1 }]);
    expect(doc.values["source1"]).toEqual({ path: "C:/m/city.duckdb" });
    expect(doc.layout["source1"]).toEqual({ x: 20, y: 40 });
    expect(store.getState().selection).toEqual(["source1"]);
    store.dispatch({ type: "undo" });
    expect(store.getState().document.structure.nodes).toEqual([]);
  });

  it("reports a file it cannot use, and a viewer ignores the drop", () => {
    const first = mountGestures();
    first.drop({ name: "notes.txt" });
    expect(first.errors).toEqual(["notes.txt is not a .duckdb, .bos or .ifc file"]);
    expect(first.store.getState().document.structure.nodes).toEqual([]);
    const viewer = mountGestures(true);
    viewer.drop({ name: "city.duckdb" });
    expect(viewer.store.getState().document.structure.nodes).toEqual([]);
  });
});

describe("the path box offers the source that holds its path", () => {
  function controls(match: (v: string) => { id: string; endpoint: string } | null) {
    const use = vi.fn();
    const files = new FilePathControls(() => Promise.resolve({ items: [] } as never), () => {}, {
      document, match: (_n, _p, value) => match(value), use,
    });
    const el = files.get("q1", "path", "C:/m/city.duckdb", ".duckdb");
    document.body.append(el);
    const button = el.querySelector<HTMLButtonElement>('button[aria-label="q1 path use source"]')!;
    return { el, button, use, input: el.querySelector("input")! };
  }
  const city = { id: "db", endpoint: "db.source" };

  it("shows 'use <source>' for a matching path and hides it otherwise", () => {
    const { button } = controls((v) => (v === "C:/m/city.duckdb" ? city : null));
    expect(button.hidden).toBe(false);
    expect(button.textContent).toBe("use db");
    expect(controls(() => null).button.hidden).toBe(true);
  });

  it("follows what is typed before it is committed", () => {
    const { button, input } = controls((v) => (v === "C:/m/city.duckdb" ? city : null));
    input.focus();
    input.value = "C:/m/other.duckdb";
    input.dispatchEvent(new Event("input"));
    expect(button.hidden).toBe(true);
    input.value = "C:/m/city.duckdb";
    input.dispatchEvent(new Event("input"));
    expect(button.hidden).toBe(false);
  });

  it("links the row on accept", () => {
    const { button, use } = controls(() => city);
    button.click();
    expect(use).toHaveBeenCalledWith("q1", "path", city);
  });

  it("is wired into the canvas instance: accepting dispatches a connect, the stored string stays", () => {
    const store = queryGraph();
    store.dispatch({ type: "addNode", id: "db", kind: "duck.source", version: 1 });
    store.dispatch({ type: "setParam", nodeId: "db", name: "path", value: "C:/m/city.duckdb" });
    const instance = createCanvasInstance({ document });
    const dispatched: unknown[] = [];
    instance.dispatch = (intent) => dispatched.push(intent);
    instance.sourceMatcher = (nodeId, param, value) =>
      param === "path" && nodeId !== "db" ? findSourceByPath(store.getState().document, catalog, value, "Database") : null;
    const el = instance.filePaths.get("q1", "path", "C:/m/city.duckdb", ".duckdb");
    el.querySelector<HTMLButtonElement>("button[aria-label='q1 path use source']")!.click();
    expect(dispatched).toEqual([{ kind: "connect", a: anchorId("out", "db", "source"), b: anchorId("in", "q1", "path") }]);
    expect(store.getState().document.values["q1"]).toEqual({ path: "C:/m/city.duckdb" });
    const other = instance.filePaths.get("q3", "path", "C:/m/other.duckdb", ".duckdb");
    expect(other.querySelector<HTMLButtonElement>("button[aria-label='q3 path use source']")!.hidden).toBe(true);
  });
});
