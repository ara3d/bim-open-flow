import { describe, expect, it, vi } from "vitest";
import type { NodeDescriptor, PortDescriptor, TableSlice } from "@bimopenflow/contracts";
import { createStore, type State, type Store } from "@bimopenflow/state";
import {
  absentReason,
  countTargets,
  COUNT_CONCURRENCY,
  createPortResults,
  PEEK_ROWS,
  watchEvaluations,
} from "../src/portResults.js";

// --- fixtures -------------------------------------------------------------

const out = (name: string, type: PortDescriptor["type"] = "Table"): PortDescriptor => ({
  name,
  type,
  optional: false,
});

function desc(outputs: PortDescriptor[], inputs: PortDescriptor[] = []): NodeDescriptor {
  return { kind: "k", version: 1, capability: "Pure", inputs, outputs, params: [], description: "" };
}

/** src (rel.csv, Relation out) -> tall (rel.filter, Relation out) -> mat
 *  (rel.materialize, Table out) -> out (sink.exportCsv, EffectPending);
 *  plus an unrelated Integer wire k.out -> n.in. Matches the plan's
 *  countTargets worked example. */
const catalog = new Map<string, NodeDescriptor>([
  ["rel.csv", desc([out("relation", "Relation")])],
  ["rel.filter", desc([out("relation", "Relation")], [out("input", "Relation")])],
  ["rel.materialize", desc([out("table", "Table")], [out("input", "Relation")])],
  ["sink.exportCsv", desc([out("out", "Any")], [out("in", "Table")])],
  ["int.const", desc([out("out", "Integer")])],
  ["int.sink", desc([], [out("in", "Integer")])],
]);

function buildGraph(store: Store): void {
  store.dispatch({ type: "addNode", id: "src", kind: "rel.csv", version: 1 });
  store.dispatch({ type: "addNode", id: "tall", kind: "rel.filter", version: 1 });
  store.dispatch({ type: "addNode", id: "mat", kind: "rel.materialize", version: 1 });
  store.dispatch({ type: "addNode", id: "out", kind: "sink.exportCsv", version: 1 });
  store.dispatch({ type: "addNode", id: "k", kind: "int.const", version: 1 });
  store.dispatch({ type: "addNode", id: "n", kind: "int.sink", version: 1 });
  store.dispatch({ type: "connect", from: "src.relation", to: "tall.input" });
  store.dispatch({ type: "connect", from: "tall.relation", to: "mat.input" });
  store.dispatch({ type: "connect", from: "mat.table", to: "out.in" });
  store.dispatch({ type: "connect", from: "k.out", to: "n.in" });
}

function statusUpdate(store: Store, statuses: Record<string, string>, errors: Record<string, string> = {}): void {
  store.dispatch({
    type: "applyServerState",
    update: {
      analysisId: "an1",
      nodes: Object.entries(statuses).map(([nodeId, status]) => ({
        nodeId,
        status: status as never,
        warnings: [],
        ...(errors[nodeId] ? { error: errors[nodeId] } : {}),
      })),
    },
  });
}

function makeOkGraph(): { store: Store; state: State } {
  const store = createStore();
  buildGraph(store);
  statusUpdate(store, { src: "Ok", tall: "Ok", mat: "Ok", out: "EffectPending", k: "Ok", n: "Ok" });
  return { store, state: store.getState() };
}

interface PendingRead {
  readonly nodeId: string;
  readonly port: string;
  readonly skip: number;
  readonly take: number;
  resolve(slice: TableSlice): void;
  reject(err: unknown): void;
}

/** An api object shaped like ResultApi but with only getResult, so nothing
 *  else can be called; reads never resolve on their own so tests control
 *  ordering explicitly. */
function fakeApi() {
  const calls: PendingRead[] = [];
  const api = {
    getResult(nodeId: string, port: string, skip: number, take: number): Promise<TableSlice> {
      return new Promise<TableSlice>((resolve, reject) => {
        calls.push({ nodeId, port, skip, take, resolve, reject });
      });
    },
  };
  const read = (nodeId: string, port: string, skip: number, take: number) => api.getResult(nodeId, port, skip, take);
  return { api, read, calls };
}

const slice = (totalRows: number, rows: unknown[][] = []): TableSlice => ({
  columns: [{ name: "id", type: "Integer" }],
  rows,
  totalRows,
  skip: 0,
});

async function flush(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

// --- countTargets / absentReason ------------------------------------------

describe("countTargets", () => {
  it("matches the worked example: table/relation outputs of Ok nodes, in edge order", () => {
    const { state } = makeOkGraph();
    expect(countTargets(state, catalog)).toEqual(["src.relation", "tall.relation", "mat.table"]);
  });

  it("drops a target whose source node is not Ok", () => {
    const { store, state } = makeOkGraph();
    statusUpdate(store, { mat: "Unready" });
    expect(countTargets(store.getState(), catalog)).toEqual(["src.relation", "tall.relation"]);
    void state;
  });
});

describe("absentReason", () => {
  it("is null for an Ok node", () => {
    const { state } = makeOkGraph();
    expect(absentReason(state, "src.relation")).toBeNull();
  });

  it("explains an EffectPending node", () => {
    const { state } = makeOkGraph();
    expect(absentReason(state, "out.out")).toBe("Writes on Run; no rows until the graph runs");
  });

  it("falls back to the node's error, else its status name", () => {
    const store = createStore();
    buildGraph(store);
    statusUpdate(store, { mat: "Error" }, { mat: "bad plan" });
    expect(absentReason(store.getState(), "mat.table")).toBe("bad plan");
    statusUpdate(store, { k: "Unavailable" });
    expect(absentReason(store.getState(), "k.out")).toBe("Unavailable");
  });

  it("is 'Not evaluated yet' with no state", () => {
    const store = createStore();
    buildGraph(store);
    expect(absentReason(store.getState(), "src.relation")).toBe("Not evaluated yet");
  });
});

// --- createPortResults: counts ---------------------------------------------

describe("createPortResults counts", () => {
  it("reads each target once, with take 0", () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.evaluated(state, catalog);
    const takenZero = calls.filter((c) => c.take === 0);
    expect(takenZero.length).toBeLessThanOrEqual(3);
    for (const c of calls) expect(c.take).toBe(0);
  });

  it("marks counts not current on a second evaluation, until the new responses land", async () => {
    const { read, calls } = fakeApi();
    const onChange = vi.fn();
    const results = createPortResults(read, onChange);
    const { state } = makeOkGraph();
    results.evaluated(state, catalog);
    // Resolve every pending read as it appears, in order, until src.relation's is answered.
    while (!calls.some((c) => c.nodeId === "src" && c.port === "relation")) {
      calls.shift()?.resolve(slice(1));
      await flush();
    }
    const first = calls.find((c) => c.nodeId === "src" && c.port === "relation")!;
    first.resolve(slice(2));
    await flush();
    expect(results.view().counts.get("src.relation")).toEqual({ rows: 2, current: true });

    results.evaluated(store2State(state), catalog);
    expect(results.view().counts.get("src.relation")).toEqual({ rows: 2, current: false });
  });

  function store2State(state: State): State {
    return state;
  }

  it("drops a response from an older generation", async () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.evaluated(state, catalog);
    const stale = calls.find((c) => c.nodeId === "src" && c.port === "relation")!;
    results.evaluated(state, catalog); // bumps the generation before `stale` resolves
    stale.resolve(slice(999));
    await flush();
    expect(results.view().counts.get("src.relation")?.rows).not.toBe(999);
  });

  it("keeps at most COUNT_CONCURRENCY count reads pending, while a hover read starts at once", () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.evaluated(state, catalog);
    const countCalls = calls.filter((c) => c.take === 0);
    expect(countCalls.length).toBe(COUNT_CONCURRENCY);

    results.hover("mat.table", state);
    const peekCalls = calls.filter((c) => c.take === PEEK_ROWS);
    expect(peekCalls).toHaveLength(1);
    expect(peekCalls[0]).toMatchObject({ nodeId: "mat", port: "table" });
  });

  it("gives absent with the reason, and no read, for a node that is not Ok", () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.hover("out.out", state);
    expect(calls).toHaveLength(0);
    expect(results.view().peek).toEqual({
      endpoint: "out.out",
      pinned: false,
      peek: { kind: "absent", reason: "Writes on Run; no rows until the graph runs" },
    });
  });
});

// --- createPortResults: peek ------------------------------------------------

describe("createPortResults peek", () => {
  it("gives absent with the error message on a rejected read", async () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.hover("src.relation", state);
    calls[0]!.reject(new Error("host unreachable"));
    await flush();
    expect(results.view().peek).toEqual({
      endpoint: "src.relation",
      pinned: false,
      peek: { kind: "absent", reason: "host unreachable" },
    });
  });

  it("ignores later hovers once pinned, and pin(null) closes the card", async () => {
    const { read, calls } = fakeApi();
    const results = createPortResults(read, () => {});
    const { state } = makeOkGraph();
    results.hover("src.relation", state);
    calls[0]!.resolve(slice(2, [[1]]));
    await flush();

    results.pin("src.relation", state);
    expect(results.view().peek?.pinned).toBe(true);

    results.hover("tall.relation", state);
    expect(results.view().peek?.endpoint).toBe("src.relation");
    expect(calls.filter((c) => c.nodeId === "tall")).toHaveLength(0);

    results.hover(null, state);
    expect(results.view().peek?.endpoint).toBe("src.relation");

    results.pin(null, state);
    expect(results.view().peek).toBeNull();
  });
});

// --- watchEvaluations --------------------------------------------------------

describe("watchEvaluations", () => {
  it("fires on evalState changes and not on selection changes", () => {
    const store = createStore();
    buildGraph(store);
    const evaluated = vi.fn();
    const results = { view: () => ({ counts: new Map(), peek: null }), evaluated, hover: () => {}, pin: () => {}, dispose: () => {} };
    const unsubscribe = watchEvaluations(store, () => catalog, results);

    store.dispatch({ type: "select", ids: ["src"] });
    expect(evaluated).not.toHaveBeenCalled();

    statusUpdate(store, { src: "Ok" });
    expect(evaluated).toHaveBeenCalledTimes(1);
    expect(evaluated).toHaveBeenCalledWith(store.getState(), catalog);

    unsubscribe();
    statusUpdate(store, { tall: "Ok" });
    expect(evaluated).toHaveBeenCalledTimes(1);
  });
});
