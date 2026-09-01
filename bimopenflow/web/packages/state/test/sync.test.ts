import { afterEach, describe, expect, it, vi } from "vitest";
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

describe("autosave", () => {
  afterEach(() => vi.useRealTimers());

  const countPuts = (calls: string[]) => calls.filter((c) => c.startsWith("putAnalysis")).length;

  it("saves after the debounce and clears dirty", async () => {
    vi.useFakeTimers();
    const fake = fakeApi();
    const store = createStore();
    await connectAnalysis(store, fake.api, "an1", { autosaveMs: 400 });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "n.bos" });
    expect(countPuts(fake.calls)).toBe(0);
    await vi.advanceTimersByTimeAsync(399);
    expect(countPuts(fake.calls)).toBe(0);
    await vi.advanceTimersByTimeAsync(1);
    expect(countPuts(fake.calls)).toBe(1);
    expect(store.getState().dirty).toBe(false);
    expect(parseDocument(fake.getSaved()!).values).toEqual({ a: { path: "n.bos" } });
  });

  it("coalesces rapid edits into one PUT", async () => {
    vi.useFakeTimers();
    const fake = fakeApi();
    const store = createStore();
    await connectAnalysis(store, fake.api, "an1", { autosaveMs: 400 });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "1.bos" });
    await vi.advanceTimersByTimeAsync(200);
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "2.bos" });
    await vi.advanceTimersByTimeAsync(399);
    expect(countPuts(fake.calls)).toBe(0);
    await vi.advanceTimersByTimeAsync(1);
    expect(countPuts(fake.calls)).toBe(1);
    expect(parseDocument(fake.getSaved()!).values).toEqual({ a: { path: "2.bos" } });
  });

  it("re-saves when an edit raced an in-flight PUT, and stays dirty meanwhile", async () => {
    vi.useFakeTimers();
    const fake = fakeApi();
    let release!: () => void;
    const original = fake.api.putAnalysis.bind(fake.api);
    fake.api.putAnalysis = async (id, body) => {
      await new Promise<void>((resolve) => (release = resolve));
      return original(id, body);
    };
    const store = createStore();
    await connectAnalysis(store, fake.api, "an1", { autosaveMs: 100 });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "1.bos" });
    await vi.advanceTimersByTimeAsync(100); // PUT now blocked in flight
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "2.bos" });
    release();
    await vi.advanceTimersByTimeAsync(0);
    expect(store.getState().dirty).toBe(true); // stale PUT must not mark saved
    await vi.advanceTimersByTimeAsync(100);
    release();
    await vi.advanceTimersByTimeAsync(0);
    expect(countPuts(fake.calls)).toBe(2);
    expect(store.getState().dirty).toBe(false);
    expect(parseDocument(fake.getSaved()!).values).toEqual({ a: { path: "2.bos" } });
  });

  it("reports a failed autosave and does not retry until the next edit", async () => {
    vi.useFakeTimers();
    const fake = fakeApi();
    fake.api.putAnalysis = async () => {
      throw new Error("boom");
    };
    const errors: unknown[] = [];
    const store = createStore();
    await connectAnalysis(store, fake.api, "an1", {
      autosaveMs: 100,
      onSaveError: (e) => errors.push(e),
    });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "1.bos" });
    await vi.advanceTimersByTimeAsync(500);
    expect(errors.length).toBe(1);
    expect(store.getState().dirty).toBe(true);
  });

  it("dispose cancels a pending autosave", async () => {
    vi.useFakeTimers();
    const fake = fakeApi();
    const store = createStore();
    const connection = await connectAnalysis(store, fake.api, "an1", { autosaveMs: 100 });
    store.dispatch({ type: "setParam", nodeId: "a", name: "path", value: "1.bos" });
    connection.dispose();
    await vi.advanceTimersByTimeAsync(500);
    expect(countPuts(fake.calls)).toBe(0);
  });
});
