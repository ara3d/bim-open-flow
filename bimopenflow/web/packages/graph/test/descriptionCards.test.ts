// TKT-278: the cards follow the page's description level. A card shows the
// part of its kind's description the level names, is as tall as that text
// wrapped, and autoLayout and the placement of a new node use that height.

import { afterEach, describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore, type Store } from "@bimopenflow/state";
import { autoLayout } from "../src/autoLayout.js";
import { defaultDescriptionLevel, setDescriptionLevel, type DescriptionLevel } from "../src/descriptionLevel.js";
import { nodeFootprint } from "../src/nodeRender.js";
import { layoutIssues } from "../src/layoutCheck.js";
import { buildCanvasModel, newNodeSize, type CanvasModel } from "../src/viewModel.js";

afterEach(() => setDescriptionLevel(defaultDescriptionLevel));

const description =
  "Keeps the rows whose value in one column matches a condition. The others are dropped. " +
  "A blank condition keeps every row, and an unknown column is a warning before any run.";

const filter: NodeDescriptor = {
  kind: "k.filter",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description,
};
const catalog = new Map([[filter.kind, filter]]);

type Positions = ReturnType<typeof autoLayout>;

function twoNodes(): Store {
  const store = createStore();
  store.dispatch({ type: "addNode", id: "a", kind: filter.kind, version: 1 });
  store.dispatch({ type: "addNode", id: "b", kind: filter.kind, version: 1 });
  store.dispatch({ type: "connect", from: "a.out", to: "b.in" });
  return store;
}

const nodeAt = (store: Store, level: DescriptionLevel) =>
  buildCanvasModel(store.getState(), catalog, null, undefined, { descriptions: level }).nodes[0]!;

describe("a card at each description level", () => {
  const store = twoNodes();

  it("shows no text, the first two sentences, or the whole description", () => {
    expect(nodeAt(store, "hidden").description).toBeUndefined();
    expect(nodeAt(store, "brief").description).toBe("Keeps the rows whose value in one column matches a condition. The others are dropped.");
    expect(nodeAt(store, "full").description).toBe(description);
  });

  it("grows from hidden to brief to full", () => {
    const [hidden, brief, full] = (["hidden", "brief", "full"] as const).map((level) => nodeAt(store, level).h);
    expect(brief).toBeGreaterThan(hidden!);
    expect(full).toBeGreaterThan(brief!);
  });

  it("follows the page's level when the caller names none", () => {
    setDescriptionLevel("full");
    expect(buildCanvasModel(store.getState(), catalog).nodes[0]!.h).toBe(nodeAt(store, "full").h);
    expect(newNodeSize(filter, "c").h).toBe(nodeAt(store, "full").h);
    setDescriptionLevel("hidden");
    expect(newNodeSize(filter, "c").h).toBe(nodeAt(store, "hidden").h);
  });

  it("gives a graph input's card its label only when descriptions show", () => {
    const withInput = createStore();
    withInput.dispatch({ type: "declareInput", input: { name: "model", kind: "FilePath", default: "" } });
    const input = (level: DescriptionLevel) =>
      buildCanvasModel(withInput.getState(), catalog, null, undefined, { descriptions: level }).nodes[0]!;
    expect(input("hidden").description).toBeUndefined();
    expect(input("brief").description).toBe("Graph input");
    expect(input("brief").h).toBeGreaterThan(input("hidden").h);
  });
});

describe("autoLayout at the full level", () => {
  /** One source feeding three siblings, which share a column. */
  function fanOut(): Store {
    const store = createStore();
    for (const id of ["src", "a", "b", "c"]) store.dispatch({ type: "addNode", id, kind: filter.kind, version: 1 });
    for (const id of ["a", "b", "c"]) store.dispatch({ type: "connect", from: "src.out", to: `${id}.in` });
    return store;
  }
  const modelAt = (store: Store, level: DescriptionLevel) =>
    buildCanvasModel(store.getState(), catalog, null, undefined, { descriptions: level });
  const placed = (model: CanvasModel, positions: Positions): CanvasModel =>
    ({ ...model, nodes: model.nodes.map((n) => ({ ...n, ...positions[n.id]! })) });
  const overlaps = (model: CanvasModel) => layoutIssues(model).filter((i) => i.kind === "overlap");

  it("keeps the cards apart by their full height", () => {
    const full = modelAt(fanOut(), "full");
    expect(nodeFootprint(full.nodes[0]!).h).toBe(full.nodes[0]!.h);
    expect(overlaps(placed(full, autoLayout(full)))).toEqual([]);
  });

  it("would overlap them had it laid them out at the hidden level's sizes", () => {
    const store = fanOut();
    const positions = autoLayout(modelAt(store, "hidden"));
    expect(overlaps(placed(modelAt(store, "full"), positions))).not.toEqual([]);
  });
});
