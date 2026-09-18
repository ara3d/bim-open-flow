import { describe, expect, it } from "vitest";
import { createStore, emptyDocument, serializeDocument } from "@bimopenflow/state";
import { primaryNodeId, reopenKeepingSelection } from "../src/selection.js";

const documentWith = (...ids: string[]): string => {
  const store = createStore();
  for (const id of ids) store.dispatch({ type: "addNode", id, kind: "test.const", version: 1 });
  return serializeDocument(store.getState().document);
};

/** A reopen replaces the document (and so clears the selection), as connectAnalysis does. */
const reopenWith = (store: ReturnType<typeof createStore>, json: string) => async () => {
  store.dispatch({ type: "setDocument", json });
};

describe("primaryNodeId", () => {
  it("returns the last selected id that is a graph node", () => {
    const store = createStore();
    store.dispatch({ type: "setDocument", json: documentWith("a", "b") });
    store.dispatch({ type: "select", ids: ["a", "b", "result-7"] });
    expect(primaryNodeId(store.getState())).toBe("b");
  });

  it("is null without a selected node", () => {
    const store = createStore();
    store.dispatch({ type: "setDocument", json: documentWith("a") });
    expect(primaryNodeId(store.getState())).toBeNull();
  });
});

describe("reopenKeepingSelection", () => {
  it("reselects the node that was selected before the reopen", async () => {
    const store = createStore();
    store.dispatch({ type: "setDocument", json: documentWith("a", "answer") });
    store.dispatch({ type: "select", ids: ["answer"] });
    const restored = await reopenKeepingSelection(store, reopenWith(store, documentWith("a", "answer")));
    expect(restored).toBe("answer");
    expect(store.getState().selection).toEqual(["answer"]);
  });

  it("leaves the selection empty when the node is gone after the reopen", async () => {
    const store = createStore();
    store.dispatch({ type: "setDocument", json: documentWith("a", "answer") });
    store.dispatch({ type: "select", ids: ["answer"] });
    const restored = await reopenKeepingSelection(store, reopenWith(store, documentWith("a")));
    expect(restored).toBeNull();
    expect(store.getState().selection).toEqual([]);
  });

  it("does nothing when nothing was selected", async () => {
    const store = createStore();
    store.dispatch({ type: "setDocument", json: documentWith("a") });
    const restored = await reopenKeepingSelection(store, reopenWith(store, serializeDocument(emptyDocument)));
    expect(restored).toBeNull();
    expect(store.getState().selection).toEqual([]);
  });
});
