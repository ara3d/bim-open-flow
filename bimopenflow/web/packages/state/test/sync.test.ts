import { describe, expect, it } from "vitest";
import type { EvalUpdate } from "@bimopenflow/contracts";
import type { ApiClient } from "@bimopenflow/api-client";
import { connectAnalysis, type AnalysisApi } from "../src/sync.js";
import { createStore } from "../src/store.js";
import { parseDocument } from "../src/document.js";

const docJson = JSON.stringify({
  formatVersion: "0.1.0",
  structure: { nodes: [{ id: "a", kind: "source.model", version: 1 }], edges: [] },
  values: { a: { path: "m.bos" } },
});

function fakeApi() {
  const calls: string[] = [];
  let emit: ((e: EvalUpdate) => void) | undefined;
  let saved: string | undefined;
  let disposed = false;
  const api: AnalysisApi = {
    getAnalysis: async (id) => {
      calls.push(`getAnalysis:${id}`);
      return docJson;
    },
    getAnalysisState: async (id) => {
      calls.push(`getAnalysisState:${id}`);
      return { analysisId: id, nodes: [{ nodeId: "a", status: "Ok", warnings: [] }] };
    },
    putAnalysis: async (id, body) => {
      calls.push(`putAnalysis:${id}`);
      saved = body;
      return { id, graphHash: "h" };
    },
    analysisEvents: (id, onEvent) => {
      calls.push(`analysisEvents:${id}`);
      emit = onEvent;
      return () => {
        disposed = true;
      };
    },
  };
  return {
    api,
    calls,
    emit: (e: EvalUpdate) => emit!(e),
    getSaved: () => saved,
    isDisposed: () => disposed,
  };
}

describe("connectAnalysis", () => {
  it("loads the document and initial eval state, then subscribes to events", async () => {
    const { api, calls } = fakeApi();
    const store = createStore();
    await connectAnalysis(store, api, "an1");
    expect(calls).toEqual(["getAnalysis:an1", "getAnalysisState:an1", "analysisEvents:an1"]);
    expect(store.getState().document.structure.nodes.map((n) => n.id)).toEqual(["a"]);
    expect(store.getState().evalState.a?.status).toBe("Ok");
    expect(store.getState().dirty).toBe(false);
  });

  it("dispatches streamed updates into evalState", async () => {
    const fake = fakeApi();
    const store = createStore();
    await connectAnalysis(store, fake.api, "an1");
    fake.emit({ analysisId: "an1", nodes: [{ nodeId: "a", status: "Error", error: "boom", warnings: [] }] });
    expect(store.getState().evalState.a?.status).toBe("Error");
  });

  it("save puts the serialized document and clears dirty", async () => {
    const fake = fakeApi();
    const store = createStore();
    const connection = await connectAnalysis(store, fake.api, "an1");
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "n.bos" });
    expect(store.getState().dirty).toBe(true);
    await connection.save();
    expect(store.getState().dirty).toBe(false);
    const saved = parseDocument(fake.getSaved()!);
    expect(saved.values).toEqual({ a: { path: "n.bos" } });
  });

  it("dispose unsubscribes from the event stream", async () => {
    const fake = fakeApi();
    const connection = await connectAnalysis(createStore(), fake.api, "an1");
    expect(fake.isDisposed()).toBe(false);
    connection.dispose();
    expect(fake.isDisposed()).toBe(true);
  });

  it("accepts a real ApiClient structurally", () => {
    const check: AnalysisApi = null as unknown as ApiClient;
    expect(check).toBeNull();
  });
});
