import { describe, expect, it } from "vitest";
import { createStore } from "../src/store.js";

describe("createStore", () => {
  it("starts with the initial state", () => {
    const store = createStore();
    expect(store.getState().document.structure.nodes).toEqual([]);
    expect(store.getState().dirty).toBe(false);
  });

  it("notifies each listener once per state-changing dispatch", () => {
    const store = createStore();
    let calls = 0;
    store.subscribe(() => calls++);
    store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
    store.dispatch({ type: "select", ids: ["a"] });
    expect(calls).toBe(2);
  });

  it("does not notify when the state is unchanged (undo on empty stack)", () => {
    const store = createStore();
    let calls = 0;
    store.subscribe(() => calls++);
    store.dispatch({ type: "undo" });
    expect(calls).toBe(0);
  });

  it("listeners see the new state and unsubscribe stops notifications", () => {
    const store = createStore();
    let seen = 0;
    const unsubscribe = store.subscribe(() => {
      seen = store.getState().document.structure.nodes.length;
    });
    store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
    expect(seen).toBe(1);
    unsubscribe();
    store.dispatch({ type: "addNode", id: "b", kind: "k", version: 1 });
    expect(seen).toBe(1);
  });

  it("a throwing action leaves the state unchanged", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
    const before = store.getState();
    expect(() => store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 })).toThrow();
    expect(store.getState()).toBe(before);
  });
});
