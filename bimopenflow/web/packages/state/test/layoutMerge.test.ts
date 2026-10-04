import { describe, expect, it } from "vitest";
import { createStore } from "../src/index.js";

describe("setLayout merging", () => {
  it("a position-only write preserves a stored width/height", () => {
    const store = createStore();
    store.dispatch({ type: "addNode", id: "a", kind: "k", version: 1 });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 1, y: 2, w: 240, h: 120 } });
    store.dispatch({ type: "setLayout", nodeId: "a", layout: { x: 30, y: 40 } });
    expect(store.getState().document.layout["a"]).toEqual({ x: 30, y: 40, w: 240, h: 120 });
  });
});
