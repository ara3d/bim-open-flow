// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDescriptor, TableSlice } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { createPeekWiring } from "../src/peekWiring.js";
import { HOVER_DELAY_MS } from "../src/portHover.js";
import { buildCanvasModel } from "../src/viewModel.js";

const catalog = new Map<string, NodeDescriptor>([
  ["k.a", {
    kind: "k.a", version: 1, capability: "Pure", inputs: [], params: [], description: "",
    outputs: [{ name: "out", type: "Table", optional: false }],
  }],
]);

const slice: TableSlice = { columns: [{ name: "id", type: "Integer" }], rows: [], totalRows: 142, skip: 0 };

beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { document.body.replaceChildren(); vi.useRealTimers(); });

function setup(viewport = { zoom: 1, pan: { x: 0, y: 0 } }) {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: "k.a", version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: "k.a", version: 1 });
  store.dispatch({ type: "connect", from: "a.out", to: "b.out" });
  const canvas = document.createElement("canvas");
  document.body.append(canvas);
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({ left: 0, top: 0 } as DOMRect);
  const onChange = vi.fn();
  let wiring!: ReturnType<typeof createPeekWiring>;
  const getDoc = () => buildCanvasModel(store.getState(), catalog, null, wiring.view());
  wiring = createPeekWiring(canvas, {
    store, getCatalog: () => catalog, readPort: () => Promise.resolve(slice),
    getDoc, getViewport: () => viewport, onChange,
  });
  return { store, canvas, onChange, wiring, getDoc };
}

const evaluate = (store: ReturnType<typeof createStore>) => store.dispatch({
  type: "applyServerState",
  update: { analysisId: "x", nodes: [{ nodeId: "a", status: "Ok", warnings: [] }] },
});

describe("createPeekWiring", () => {
  it("delivers a row count after an evaluation and notifies", async () => {
    const { store, wiring, onChange } = setup();
    evaluate(store);
    await vi.advanceTimersByTimeAsync(0);
    expect(wiring.view().counts.get("a.out")).toEqual({ rows: 142, current: true });
    expect(onChange).toHaveBeenCalled();
  });

  it("peeks the endpoint under a pointer resting 300 ms, through the viewport", async () => {
    const { store, canvas, wiring, getDoc } = setup({ zoom: 2, pan: { x: 10, y: 20 } });
    evaluate(store);
    await vi.advanceTimersByTimeAsync(0);
    const node = getDoc().nodes.find((n) => n.id === "a")!;
    // Socket world position (right edge, first port row) -> CSS pixels.
    const wx = node.x + node.w;
    const wy = node.y + 46 + 0.5 * 24;
    canvas.dispatchEvent(new MouseEvent("pointermove", { clientX: wx * 2 + 10, clientY: wy * 2 + 20, bubbles: true }));
    expect(wiring.view().peek).toBeNull();
    await vi.advanceTimersByTimeAsync(HOVER_DELAY_MS);
    expect(wiring.view().peek?.endpoint).toBe("a.out");
  });

  it("removes its listeners on dispose", async () => {
    const { store, canvas, wiring, getDoc } = setup();
    const node = getDoc().nodes.find((n) => n.id === "a")!;
    wiring.dispose();
    canvas.dispatchEvent(new MouseEvent("pointermove", {
      clientX: node.x + node.w, clientY: node.y + 46 + 12, bubbles: true }));
    await vi.advanceTimersByTimeAsync(HOVER_DELAY_MS);
    expect(wiring.view().peek).toBeNull();
    evaluate(store);
    await vi.advanceTimersByTimeAsync(0);
    expect(wiring.view().counts.size).toBe(0);
  });
});
